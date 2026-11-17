// 기술 문서 편집 화면 — 왼쪽 원문(.md)을 고치면 오른쪽 상세 기술문서 미리보기가 바로 바뀝니다. 저장은 편집 서버(/__edit/file)가 합니다.
//   미리보기 = doc.html?p=<폴더>&pf-edit=frame (editor/edit.js의 frameDoc이 고친 글로 다시 그림)
//   원문 줄 ↔ 미리보기 블록: 미리보기와 같은 marked(12.0.2)로 원문을 블록으로 나눠 줄 번호를 셉니다.
(async () => {
  'use strict';
  const $ = (id) => document.getElementById(id);
  const Q = new URLSearchParams(location.search);
  const status = (text, cls = '') => { $('status').textContent = text; $('status').className = 'pfe-status' + (cls ? ' ' + cls : ''); };
  const api = async (name, body, raw) => {
    const res = await fetch('/__edit/' + name, body === undefined ? { cache: 'no-store' }
      : { method: 'POST', headers: raw ? { 'X-PF-Edit': '1' } : { 'Content-Type': 'application/json', 'X-PF-Edit': '1' }, body: raw ? body : JSON.stringify(body) });
    const j = await res.json().catch(() => ({ error: `편집 서버 응답을 읽지 못했습니다(HTTP ${res.status})` }));
    if (!res.ok) throw Object.assign(new Error(j.error || `HTTP ${res.status}`), j, { status: res.status });
    return j;
  };

  let st;
  try { st = await api('state'); }
  catch { status('편집 서버로 열어야 합니다 → 레포 폴더에서 python editor/server.py 실행 뒤 http://localhost:4601', 'pfe-bad'); return; }
  const pid = st.projects.includes(Q.get('p')) ? Q.get('p') : st.projects[0];
  if (!pid || !st.docs[pid]) { status('src/ 폴더에 기술 문서(.md)가 있는 프로젝트가 없습니다', 'pfe-bad'); return; }
  const rel = `src/${pid}/${st.docs[pid]}`;
  document.title = `${pid} — 기술 문서 고치기`;
  $('back').href = `/doc.html?p=${encodeURIComponent(pid)}`;
  $('proj').append(...st.projects.filter((id) => st.docs[id]).map((id) => Object.assign(document.createElement('option'), { value: id, textContent: id, selected: id === pid })));
  $('proj').onchange = () => { if (!dirty || confirm('저장하지 않은 고친 글이 있습니다. 버리고 다른 문서를 열까요?')) { dirty = false; location.href = `?p=${encodeURIComponent($('proj').value)}`; } else $('proj').value = pid; };

  let file = await api('file?path=' + encodeURIComponent(rel));
  let dirty = false, saving = false, stale = false;

  // ── 편집기(CodeMirror, CDN이 막혀 있으면 보통 글상자) ──
  const ed = (() => {
    const host = $('src');
    if (window.CodeMirror) {
      const cm = CodeMirror(host, { value: file.text, mode: { name: 'markdown', fencedCodeBlockHighlighting: false }, lineNumbers: true, lineWrapping: true, indentUnit: 4, tabSize: 4, indentWithTabs: false,
        extraKeys: { Tab: (c) => (c.somethingSelected() ? c.indentSelection('add') : c.replaceSelection('    ')), 'Shift-Tab': (c) => c.indentSelection('subtract'),
          'Ctrl-S': () => save(), 'Cmd-S': () => save(), 'Ctrl-B': () => act('b'), 'Cmd-B': () => act('b'), 'Ctrl-E': () => act('c'), 'Cmd-E': () => act('c'), 'Ctrl-K': () => act('link'), 'Cmd-K': () => act('link') } });
      let marks = [];
      return { cm, get: () => cm.getValue(), set: (t) => { const s = cm.getScrollInfo(), c = cm.getCursor(); cm.setValue(t); cm.setCursor(c); cm.scrollTo(s.left, s.top); },
        onChange: (f) => cm.on('change', (_, ch) => ch.origin !== 'setValue' && f()), onCursor: (f) => cm.on('cursorActivity', f), line: () => cm.getCursor().line,
        sel: () => cm.getSelection(), replace: (t, select) => cm.replaceSelection(t, select ? 'around' : 'end'), focus: () => cm.focus(),
        lineText: (n) => cm.getLine(n) || '', setLine: (n, t) => cm.replaceRange(t, { line: n, ch: 0 }, { line: n, ch: (cm.getLine(n) || '').length }),
        goto: (a, b) => { cm.setCursor({ line: a, ch: 0 }); cm.scrollIntoView({ from: { line: a, ch: 0 }, to: { line: Math.min(b, a + 12), ch: 0 } }, 120);
          marks.forEach((l) => cm.removeLineClass(l, 'background', 'pfe-hit')); marks = [];
          for (let l = a; l < b; l++) marks.push(cm.addLineClass(l, 'background', 'pfe-hit'));
          const mine = marks; setTimeout(() => { mine.forEach((l) => cm.removeLineClass(l, 'background', 'pfe-hit')); }, 1400); cm.focus(); },
        atLineStart: () => cm.getCursor().ch === 0, el: cm.getWrapperElement() };
    }
    const ta = document.createElement('textarea');
    ta.value = file.text; ta.spellcheck = false;
    host.append(ta);
    const lineStart = (n) => ta.value.split('\n').slice(0, n).reduce((a, l) => a + l.length + 1, 0);
    ta.addEventListener('keydown', (e) => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); save(); } });
    return { get: () => ta.value, set: (t) => { const s = ta.scrollTop; ta.value = t; ta.scrollTop = s; }, onChange: (f) => ta.addEventListener('input', f), onCursor: (f) => document.addEventListener('selectionchange', () => document.activeElement === ta && f()),
      line: () => ta.value.slice(0, ta.selectionStart).split('\n').length - 1, sel: () => ta.value.slice(ta.selectionStart, ta.selectionEnd),
      replace: (t, select) => { const a = ta.selectionStart; ta.setRangeText(t, a, ta.selectionEnd, select ? 'select' : 'end'); ta.dispatchEvent(new Event('input')); }, focus: () => ta.focus(),
      lineText: (n) => ta.value.split('\n')[n] || '', setLine: (n, t) => { const a = lineStart(n); ta.setRangeText(t, a, a + (ta.value.split('\n')[n] || '').length, 'end'); ta.dispatchEvent(new Event('input')); },
      goto: (a) => { const p = lineStart(a); ta.focus(); ta.setSelectionRange(p, p); ta.scrollTop = Math.max(0, (a - 4) * parseFloat(getComputedStyle(ta).lineHeight)); },
      atLineStart: () => ta.selectionStart === 0 || ta.value[ta.selectionStart - 1] === '\n', el: ta };
  })();

  // ── 미리보기 틀 ──
  const view = $('view');
  let frame = null, map = [];                                           // map[i] = 미리보기 i번째 블록의 원문 줄 범위 { a, b } (0부터, b는 끝 다음 줄)
  const anchor = (() => { try { return decodeURIComponent(location.hash.slice(1)); } catch { return ''; } })();
  view.src = `/doc.html?p=${encodeURIComponent(pid)}&pf-edit=frame`;
  await new Promise((r) => view.addEventListener('load', r, { once: true }));
  const win = view.contentWindow;
  try { await win.PFDoc.whenReady(); } catch { /* */ }
  frame = win.PFEditFrame || null;
  if (!frame) status('미리보기를 연결하지 못했습니다(편집 서버로 열었는지 확인해 주세요). 글은 고치고 저장할 수 있습니다.', 'pfe-bad');

  // 원문 → 블록별 줄 범위. 블록 하나가 미리보기 요소 몇 개가 되는지는 같은 marked로 세어 맞춤
  function buildMap(text) {
    const m = win.marked;
    if (!m) return [];
    const out = [];
    const tokens = m.lexer(text, { gfm: true });
    const tpl = win.document.createElement('template');
    let line = 0;
    for (const t of tokens) {
      const n = (t.raw.match(/\n/g) || []).length;
      if (t.type !== 'space') {
        const one = [t]; one.links = tokens.links;
        let k = 0;
        try { tpl.innerHTML = m.parser(one); k = tpl.content.children.length; } catch { k = 0; }
        const b = line + Math.max(1, t.raw.replace(/\n+$/, '').split('\n').length);
        for (let i = 0; i < k; i++) out.push({ a: line, b, type: t.type });
      }
      line += n;
    }
    return out;
  }
  const blockAt = (ln) => { let hit = -1; map.forEach((r, i) => { if (r.a <= ln) hit = i; }); return hit; };
  const ok = () => frame && map.length && map.length === frame.count();
  function sync(scroll) { if (ok()) frame.highlight(blockAt(ed.line()), scroll); }
  function remap() { map = buildMap(ed.get()); }

  if (frame) {
    frame.setDoc(`/${rel}`);
    remap();
    frame.hooks.onRendered = () => { remap(); sync(false); };
    frame.hooks.onPick = (i, sub) => {
      if (!ok() || !map[i]) return;
      let { a, b } = map[i];
      const lines = ed.get().split('\n');
      if (sub.row != null && map[i].type === 'table') a = Math.min(b - 1, a + (sub.row === 0 ? 0 : sub.row + 1));
      else if (sub.codeLine != null && map[i].type === 'code') a = Math.min(b - 1, a + 1 + sub.codeLine);
      else if (sub.item != null) {                                      // 목록: n번째 항목이 시작하는 줄(코드 울타리 안은 건너뜀)
        let seen = -1, fence = false;
        for (let l = a; l < b; l++) {
          if (/^\s*(```|~~~)/.test(lines[l])) fence = !fence;
          if (!fence && /^\s*(?:>\s*)*(?:[-*+]|\d+[.)])\s/.test(lines[l]) && ++seen === sub.item) { a = l; break; }
        }
      }
      if (sub.row != null || sub.codeLine != null || sub.item != null) b = a + 1;
      ed.goto(a, b);
      frame.highlight(i, false);
    };
    if (anchor) { const i = frame.headingBlock(anchor); if (i >= 0 && map[i] && ok()) { ed.goto(map[i].a, map[i].b); win.document.getElementById(anchor)?.scrollIntoView({ block: 'start' }); frame.highlight(i, false); } }
  }

  // ── 고치기 → 미리보기 ──
  let timer = 0;
  const mark = () => { if (stale) return; status(dirty ? '● 고친 글이 있습니다 — 저장하려면 Ctrl+S' : '저장됨', dirty ? 'pfe-dirty' : ''); $('save').disabled = !dirty || saving; };
  ed.onChange(() => {
    dirty = true; mark();
    clearTimeout(timer);
    timer = setTimeout(() => frame?.render(ed.get()), 350);
  });
  let ctimer = 0;
  ed.onCursor(() => { clearTimeout(ctimer); ctimer = setTimeout(() => sync(true), 120); });
  addEventListener('beforeunload', (e) => { if (dirty) { e.preventDefault(); e.returnValue = ''; } });
  addEventListener('keydown', (e) => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); save(); } });
  mark();

  async function save(force = false) {
    if (saving || (!dirty && !force)) return;
    saving = true; mark(); status('저장하는 중…');
    const text = ed.get();
    try {
      const r = await api('file', { path: rel, text, baseHash: file.hash, force });
      file = { ...file, text, hash: r.hash };
      dirty = ed.get() !== text; stale = false; saving = false; mark();
      $('note').textContent = '저장했습니다. 상세 문서에는 바로 보입니다. 포트폴리오 문서·발표 덱의 요약 글(소개·기능 설명·장면의 글)은 Claude Code에서 /portfolio-build를 다시 실행해야 바뀝니다.';
    } catch (err) {
      saving = false;
      if (err.conflict && confirm('이 파일이 다른 곳(다른 편집기·Claude)에서 바뀌었습니다.\n\n[확인] 지금 화면의 글로 덮어쓰기\n[취소] 저장하지 않기([다시 읽기]로 바뀐 파일을 불러올 수 있습니다)')) return save(true);
      mark(); status(`저장하지 못했습니다: ${err.message}`, 'pfe-bad');
    }
  }
  $('save').onclick = () => save();
  async function reread() {
    file = await api('file?path=' + encodeURIComponent(rel));
    ed.set(file.text);
    dirty = false; stale = false; mark();
    frame?.render(file.text);
  }
  $('revert').onclick = () => { if (!dirty || confirm('저장하지 않은 고친 글을 버리고 파일을 다시 읽을까요?')) reread().catch((e) => status(e.message, 'pfe-bad')); };
  // 다른 편집기(Rider·VS Code)나 Claude가 파일을 고치면: 여기서 고친 것이 없을 때는 바로 따라가고, 있으면 알려 줌
  setInterval(async () => {
    if (document.hidden || saving) return;
    try {
      const now = await api('file?path=' + encodeURIComponent(rel));
      if (now.hash === file.hash) return;
      if (!dirty) { file = now; ed.set(now.text); frame?.render(now.text); mark(); }
      else if (!stale) { stale = true; status('⚠ 파일이 다른 곳에서 바뀌었습니다 — [저장]하면 덮어쓸지 묻습니다', 'pfe-bad'); }
    } catch { /* 서버가 잠깐 꺼짐 */ }
  }, 2500);

  // ── 글 단추 ──
  const TPL = {
    table: '| 항목 | 내용 |\n|---|---|\n|  |  |\n',
    code: '```csharp\n\n```\n',
    mmd: '```mermaid\nflowchart LR\n    A[시작] --> B[다음]\n```\n',
  };
  function block(text) { ed.replace((ed.atLineStart() ? '' : '\n') + '\n' + text + '\n'); ed.focus(); }
  function act(kind) {
    const s = ed.sel();
    if (kind === 'b') ed.replace(`**${s || '굵은 글'}**`, !s);
    else if (kind === 'c') ed.replace('`' + (s || '코드') + '`', !s);
    else if (kind === 'link') ed.replace(`[${s || '글자'}](https://)`, false);
    else if (kind === 'h' || kind === 'ul') {
      const n = ed.line(), t = ed.lineText(n);
      if (kind === 'ul') ed.setLine(n, /^\s*[-*+]\s/.test(t) ? t.replace(/^(\s*)[-*+]\s/, '$1') : t.replace(/^(\s*)/, '$1- '));
      else { const m = /^(#{1,6})\s+/.exec(t); const next = !m ? '## ' : m[1].length >= 4 ? '' : '#'.repeat(Math.max(3, m[1].length + 1)) + ' '; ed.setLine(n, next + t.replace(/^#{1,6}\s+/, '')); }
    } else if (TPL[kind]) block(TPL[kind]);
    else if (kind === 'img') openImages();
    ed.focus();
  }
  document.querySelector('.pfe-bar').addEventListener('click', (e) => { const b = e.target.closest('[data-do]'); if (b) act(b.dataset.do); });

  // ── 그림: 고르기 · 올리기 · 붙여 넣기 · 끌어다 놓기 ──
  const isMedia = (name) => /\.(png|jpe?g|gif|webp|svg|mp4|webm)$/i.test(name);
  const mdFor = (path) => (/\.(mp4|webm)$/i.test(path) ? `<video src="${path}" controls muted></video>` : `![${path.split('/').pop().replace(/\.[^.]+$/, '')}](${encodeURI(path)})`);
  async function upload(f, name) {
    status('그림을 올리는 중…');
    const r = await api(`upload?p=${encodeURIComponent(pid)}&name=${encodeURIComponent(name || f.name || 'image.png')}`, f, true);
    mark();
    return r.path;
  }
  async function insertFiles(files) {
    for (const f of files) {
      const ext = (f.type.split('/')[1] || 'png').replace('jpeg', 'jpg').replace('svg+xml', 'svg');
      const stamp = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14);
      const name = f.name && f.name !== 'image.png' ? f.name : `paste-${stamp}.${ext}`;
      if (!isMedia(name)) { status(`올릴 수 없는 파일입니다: ${name}`, 'pfe-bad'); continue; }
      try { block(mdFor(await upload(f, name))); } catch (err) { status(`그림을 올리지 못했습니다: ${err.message}`, 'pfe-bad'); }
    }
  }
  ed.el.addEventListener('paste', (e) => { const fs = [...(e.clipboardData?.files || [])]; if (fs.length) { e.preventDefault(); e.stopPropagation(); insertFiles(fs); } }, true);
  ed.el.addEventListener('drop', (e) => { const fs = [...(e.dataTransfer?.files || [])]; if (fs.length) { e.preventDefault(); e.stopPropagation(); insertFiles(fs); } }, true);
  ed.el.addEventListener('dragover', (e) => { if (e.dataTransfer?.types?.includes('Files')) e.preventDefault(); }, true);
  async function openImages() {
    const dlg = $('imgdlg'), grid = $('imggrid');
    grid.textContent = '불러오는 중…';
    dlg.showModal();
    const { images } = await api('images?p=' + encodeURIComponent(pid));
    grid.replaceChildren(...images.map((p) => {
      const b = document.createElement('button');
      b.type = 'button';
      const m = document.createElement(/\.(mp4|webm)$/i.test(p) ? 'video' : 'img');
      m.src = `/src/${encodeURIComponent(pid)}/${p.split('/').map(encodeURIComponent).join('/')}`; m.loading = 'lazy'; m.muted = true;
      b.append(m, p.replace(/^images\//, ''));
      b.onclick = () => { dlg.close(); block(mdFor(p)); };
      return b;
    }));
    if (!images.length) grid.textContent = 'images/ 폴더에 그림이 없습니다. [+ 새 그림 올리기]로 넣어 보세요.';
  }
  $('imgclose').onclick = () => $('imgdlg').close();
  $('imgfile').onchange = async (e) => { const fs = [...e.target.files]; e.target.value = ''; $('imgdlg').close(); await insertFiles(fs); };

  // ── 가운데 띠를 끌어 폭 바꾸기 ──
  const split = $('split');
  try { const w = localStorage.getItem('pfe-split'); if (w) split.style.setProperty('--w', w); } catch { /* */ }
  $('divider').addEventListener('pointerdown', (e) => {
    e.preventDefault();
    split.classList.add('pfe-dragging');
    const move = (ev) => { const r = split.getBoundingClientRect(); split.style.setProperty('--w', `${Math.max(20, Math.min(80, ((ev.clientX - r.left) / r.width) * 100)).toFixed(1)}%`); };
    const up = () => { split.classList.remove('pfe-dragging'); removeEventListener('pointermove', move); removeEventListener('pointerup', up); ed.cm?.refresh(); try { localStorage.setItem('pfe-split', split.style.getPropertyValue('--w')); } catch { /* */ } };
    addEventListener('pointermove', move); addEventListener('pointerup', up);
  });
  ed.focus();
})();
