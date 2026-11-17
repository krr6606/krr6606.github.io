// 편집 도구 — 편집 서버(editor/server.py)가 index.html · project.html · doc.html을 내보낼 때만 끼워 넣습니다.
//   렌더러(assets/)는 고치지 않습니다. 저장은 모두 편집 서버의 /__edit/… 로 보내고, 서버가 정해진 소스 파일만 고칩니다.
//   내 정보 → profile.md(+ data/portfolio.json의 같은 칸) · 문구 → theme/strings.tsv "내 문구" 칸 · 테마 → theme/theme.css 손잡이 값
//   장면 → src/<폴더>/deck-edits.json(deck-edits.py apply) · 기술 문서 → editor/doc-edit.html(나란히 보며 고치는 화면)
(() => {
  'use strict';
  const Q = new URLSearchParams(location.search);
  const PAGE = /\/project\.html$/.test(location.pathname) ? 'deck' : /\/doc\.html$/.test(location.pathname) ? 'doc' : 'portfolio';
  if (Q.get('pf-edit') === 'frame') { if (PAGE === 'doc') frameDoc(); return; }
  if (Q.get('pf-audit') === '1' || window.top !== window) return;       // 점검용 주소·다른 틀 안에서는 도구를 띄우지 않음

  // ── 작은 도우미 ──
  const el = (tag, attrs = {}, ...kids) => {
    const n = document.createElement(tag);
    let value;
    for (const [k, v] of Object.entries(attrs || {})) {
      if (v == null || v === false) continue;
      if (k === 'class') n.className = v;
      else if (k === 'text') n.textContent = v;
      else if (k === 'value') value = v;
      else if (k.startsWith('on')) n.addEventListener(k.slice(2), v);
      else n.setAttribute(k, v === true ? '' : v);
    }
    add(n, ...kids);
    if (value !== undefined) n.value = value;
    return n;
  };
  const add = (node, ...kids) => { kids.flat(Infinity).forEach((c) => c != null && c !== false && node.append(c)); return node; };
  const fill = (node, ...kids) => { node.replaceChildren(); return add(node, ...kids); };
  const api = async (name, body) => {
    const res = await fetch('/__edit/' + name, body === undefined ? { cache: 'no-store' }
      : { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-PF-Edit': '1' }, body: JSON.stringify(body) });
    const j = await res.json().catch(() => ({ error: `편집 서버 응답을 읽지 못했습니다(HTTP ${res.status})` }));
    if (!res.ok) throw Object.assign(new Error(j.error || `HTTP ${res.status}`), j);
    return j;
  };
  const SS = 'pfe-state';
  const state = { open: false, tab: '', q: '', flash: '' };
  try { Object.assign(state, JSON.parse(sessionStorage.getItem(SS) || '{}')); } catch { /* 저장소 막힘 */ }
  const keep = () => { try { sessionStorage.setItem(SS, JSON.stringify(state)); } catch { /* */ } };
  const reload = (msg = '') => { state.flash = msg; state.open = true; keep(); location.reload(); };
  const pid = Q.get('p') || '';
  const docEditUrl = (id, anchor = '') => `/editor/doc-edit.html?p=${encodeURIComponent(id)}${anchor ? '#' + encodeURIComponent(anchor) : ''}`;
  const S = typeof PFStr !== 'undefined' ? PFStr : null;                 // 문구 표(assets/strings.js)

  // ── 뼈대 ──
  const TABS = { portfolio: [['me', '내 정보']], doc: [['doc', '문서']], deck: [['scene', '장면']] }[PAGE]
    .concat([['str', '문구'], ['theme', '테마'], ['files', '바뀐 파일']]);
  if (!TABS.some(([k]) => k === state.tab)) state.tab = TABS[0][0];
  const body = el('div', { class: 'pfe-body' });
  const tabBar = el('div', { class: 'pfe-tabs', role: 'tablist' }, TABS.map(([k, label]) => el('button', { type: 'button', role: 'tab', 'data-tab': k, text: label, onclick: () => show(k) })));
  const foot = el('div', { class: 'pfe-foot' });
  const panel = el('aside', { class: 'pfe-panel', 'aria-label': '편집 도구' },
    el('div', { class: 'pfe-head' }, el('b', { text: '편집' }),
      el('button', { type: 'button', class: 'pfe-mini', text: '화면에서 글 고르기', title: '페이지의 글을 누르면 그 글을 어디서 고치는지 찾아 줍니다', onclick: () => pick() }),
      el('button', { type: 'button', class: 'pfe-mini', text: '닫기', onclick: () => toggle(false) })),
    tabBar, body, foot);
  const fab = el('div', { class: 'pfe-fab' },
    PAGE === 'doc' && pid && el('button', { type: 'button', class: 'pfe-primary', text: '✎ 문서 고치기', onclick: () => { location.href = docEditUrl(pid, currentHeading()); } }),
    el('button', { type: 'button', text: PAGE === 'doc' ? '편집 도구' : '✎ 편집', onclick: () => toggle(true) }));
  const root = el('div', { class: 'pfe', 'data-pf-ui': 'edit' }, fab, panel);
  document.body.append(root);
  addEventListener('keydown', (e) => { if (e.key === 'Escape' && state.open && !document.querySelector('dialog[open]') && root.contains(document.activeElement)) toggle(false); });

  function toggle(on) {
    state.open = on; keep();
    root.classList.toggle('pfe-open', on);
    if (on) show(state.tab);
  }
  let drawn = 0;
  async function show(tab) {
    state.tab = tab; keep();
    tabBar.querySelectorAll('button').forEach((b) => b.setAttribute('aria-selected', String(b.dataset.tab === tab)));
    const my = ++drawn;
    const box = el('div');
    fill(body, el('p', { class: 'pfe-hint', text: '불러오는 중…' }));
    try {
      await ({ me: tabMe, str: tabStr, theme: tabTheme, files: tabFiles, doc: tabDoc, scene: tabScene }[tab])(box);
    } catch (err) { fill(box, bad(err)); }
    if (my !== drawn) return;
    fill(body, box);
    if (state.flash) { box.prepend(el('p', { class: 'pfe-flash', text: state.flash })); state.flash = ''; keep(); }
  }
  const bad = (err) => el('div', { class: 'pfe-flash pfe-bad' }, el('b', { text: '하지 못했습니다' }), el('pre', { text: String(err && err.message || err) }));
  const fail = (box, err) => { box.querySelectorAll('.pfe-flash').forEach((x) => x.remove()); box.prepend(bad(err)); body.scrollTop = 0; };

  // 아래 띠: 프로젝트마다 기술 문서 편집으로 가는 길
  api('state').then((st) => {
    fill(foot, el('span', { text: '기술 문서 고치기:' }), st.projects.filter((id) => st.docs[id]).map((id) => el('a', { href: docEditUrl(id), text: id })));
  }).catch(() => fill(foot, el('span', { text: '편집 서버에 연결하지 못했습니다' })));

  if (state.open) toggle(true);

  // ── 탭: 내 정보(profile.md) ──
  async function tabMe(box) {
    const { profile: pr, hash, projects } = await api('profile');
    const f = {};
    const row = (label, node, hint) => el('label', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: label }), node, hint && el('small', { text: hint }));
    const text = (k, label, hint) => row(label, (f[k] = el('input', { type: 'text', value: pr[k] || '' })), hint);
    f.intro = el('textarea', { rows: 5, value: pr.intro || '' });
    let order = [...pr.order.filter((x) => projects.includes(x)), ...projects.filter((x) => !pr.order.includes(x))];
    const first = order.join(',');
    const list = el('ol', { class: 'pfe-order' });
    const drawOrder = () => fill(list, ...order.map((id, i) => el('li', {}, el('span', { class: 'pfe-ell', text: `${i + 1}. ${id}` }),
      el('button', { type: 'button', class: 'pfe-mini', text: '↑', disabled: i === 0, onclick: () => { order.splice(i - 1, 0, order.splice(i, 1)[0]); drawOrder(); } }),
      el('button', { type: 'button', class: 'pfe-mini', text: '↓', disabled: i === order.length - 1, onclick: () => { order.splice(i + 1, 0, order.splice(i, 1)[0]); drawOrder(); } }))));
    drawOrder();
    const hex = /^#[0-9a-f]{6}$/i;
    const themed = getComputedStyle(document.documentElement).getPropertyValue('--t-accent').trim();
    f.accent = el('input', { type: 'text', value: pr.accent || '', class: 'pfe-grow', oninput: () => { if (hex.test(f.accent.value)) { color.value = f.accent.value; if (!themed) PF.applyTheme(f.accent.value); } } });
    const color = el('input', { type: 'color', value: hex.test(pr.accent) ? pr.accent : '#2F6FEB', oninput: () => { f.accent.value = color.value.toUpperCase(); if (!themed) PF.applyTheme(color.value); } });
    const save = el('button', { type: 'button', class: 'pfe-primary', text: '저장하고 보기', onclick: async () => {
      const fields = {};
      for (const k of ['name', 'nameEn', 'headline', 'email', 'github', 'accent', 'intro']) if (f[k].value.trim() !== (pr[k] || '').trim()) fields[k] = f[k].value.trim();
      if (order.join(',') !== first) fields.order = order;
      if (!Object.keys(fields).length) return fail(box, '바꾼 것이 없습니다');
      save.disabled = true;
      try { await api('profile', { fields, baseHash: hash }); reload('내 정보를 저장했습니다 (profile.md)'); }
      catch (err) { save.disabled = false; fail(box, err); }
    } });
    add(box, el('p', { class: 'pfe-hint', text: '여기서 고친 값은 profile.md에 저장되고 페이지에 바로 반영됩니다.' }), el('p', { class: 'pfe-h', text: '기본 정보' }),
      text('name', '이름'), text('nameEn', '영문 이름'), text('headline', '한 줄 정체성'), text('email', '이메일'), text('github', 'GitHub 주소'),
      row('자기소개', f.intro, '줄을 바꾸면 문단이 나뉩니다. 2~3문장을 권합니다.'),
      el('p', { class: 'pfe-h', text: '페이지 설정' }),
      el('div', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: '강조색' }), el('div', { class: 'pfe-line' }, color, f.accent),
        themed && el('small', { text: `지금은 테마의 강조색(${themed})이 쓰이고 있어 이 값은 보이지 않습니다. 색은 [테마] 탭에서 바꾸세요.` })),
      el('div', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: '프로젝트 순서' }), list),
      el('div', { class: 'pfe-acts' }, save),
      el('p', { class: 'pfe-hint', style: 'margin-top:14px', text: '프로젝트 소개·기능 설명·기술 스택 글은 기술 문서에서 뽑은 요약입니다. 기술 문서를 고친 뒤 Claude Code에서 /portfolio-build를 다시 실행하면 바뀝니다.' }));
  }

  // ── 탭: 문구(theme/strings.tsv의 "내 문구" 칸) ──
  async function tabStr(box) {
    if (!S) { add(box, el('p', { text: '이 페이지에는 문구 표가 없습니다.' })); return; }
    const known = S.keys();
    let rows = [];
    try {
      const file = await api('file?path=theme/strings.tsv');
      rows = file.text.split('\n').map((l) => l.split('\t').map((c) => c.trim())).filter((c) => c.length >= 2 && known.includes(c[0]))
        .map((c) => (c.length === 2 ? { key: c[0], where: '', def: '', mine: c[1] } : { key: c[0], where: c[1], def: c[2], mine: c[3] || '' }));
    } catch { /* 표가 없으면 기본 문구만 */ }
    known.filter((k) => !rows.some((r) => r.key === k)).forEach((k) => rows.push({ key: k, where: '', def: '', mine: '', fresh: true }));
    const listBox = el('div');
    const notice = el('div');
    const saved = () => fill(notice, el('div', { class: 'pfe-flash pfe-line' }, el('span', { class: 'pfe-grow', text: '저장했습니다. 새로고침하면 화면에 보입니다.' }),
      el('button', { type: 'button', class: 'pfe-mini pfe-primary', text: '새로고침', onclick: () => reload() })));
    const draw = () => {
      const q = state.q.trim().toLowerCase();
      const hit = rows.filter((r) => !q || [r.key, r.where, r.def, r.mine].some((x) => x.toLowerCase().includes(q)));
      fill(listBox, ...hit.map((r) => {
        const input = el('input', { type: 'text', value: r.mine, placeholder: r.def || '(기본 문구)', 'aria-label': `${r.key} 내 문구`, onchange: async () => {
          try { await api('string', { key: r.key, value: input.value, where: r.where, default: r.fresh ? '' : r.def }); r.mine = input.value.trim(); saved(); }
          catch (err) { fail(box, err); }
        } });
        return el('div', { class: 'pfe-card' }, el('div', { class: 'pfe-line' }, el('span', { class: 'pfe-grow', text: r.def || S.t(r.key) }), el('span', { class: 'pfe-key', text: r.key })),
          r.where && el('small', { text: r.where }), input);
      }), !hit.length && el('p', { class: 'pfe-hint', text: '찾는 문구가 없습니다.' }));
    };
    const issues = (S.report().issues || []);
    add(box, el('p', { class: 'pfe-hint', text: '절 이름·버튼·라벨 같은 화면 문구입니다. 칸에 쓰고 Enter를 누르면 저장됩니다. 비우면 기본 문구로 돌아갑니다. {n}·{name.deck} 같은 { } 자리는 지우지 마세요.' }),
      issues.length ? el('details', { class: 'pfe-flash pfe-bad' }, el('summary', { text: `문구 표에서 확인할 것 ${issues.length}건` }), el('pre', { text: issues.map((i) => `${i.line ? i.line + '번째 줄 ' : ''}${i.key}: ${i.detail}`).join('\n') })) : null,
      notice, el('div', { class: 'pfe-row' }, el('input', { type: 'search', placeholder: '문구 찾기(글자·키)', value: state.q, oninput: (e) => { state.q = e.target.value; keep(); draw(); } })), listBox);
    draw();
  }

  // ── 탭: 테마(theme/theme.css의 손잡이 값) ──
  async function tabTheme(box) {
    const file = await api('file?path=theme/theme.css');
    const vars = [];
    let group = '';
    file.text.split('\n').forEach((ln) => {
      const g = /^\s*\/\*\s*([①-⑳].*?)\s*\*\/\s*$/.exec(ln);
      if (g) { group = g[1].split(' — ')[0]; return; }
      const m = /^\s*(--t-[a-z0-9-]+)\s*:\s*([^;]+);\s*(?:\/\*\s*(.*?)\s*\*\/)?/.exec(ln);
      if (m) vars.push({ name: m[1], value: m[2].trim(), note: m[3] || '', group });
    });
    if (!vars.length) {
      add(box, el('p', { text: 'theme/theme.css에 정해진 테마 값이 없습니다(기본 테마).' }),
        el('p', { class: 'pfe-hint', text: 'Claude Code에서 /portfolio-theme에 참고할 화면을 첨부하거나 "밝은 테마로"처럼 말하면 테마가 만들어지고, 그 뒤 여기서 값을 고칠 수 있습니다.' }));
      return;
    }
    const hex = /^#[0-9a-f]{6}$/i;
    const html = document.documentElement;
    const changed = {};
    let accent = null;
    fetch('data/portfolio.json', { cache: 'no-store' }).then((r) => r.json()).then((d) => { accent = d.settings?.accent; }).catch(() => {});
    const live = (v, value) => {
      if (value === v.value) delete changed[v.name]; else changed[v.name] = value;
      html.style.setProperty(v.name, value);
      try { PF.applyTheme(accent); } catch { /* 덱이 아직 준비 전 */ }
      save.disabled = !Object.keys(changed).length;
    };
    const save = el('button', { type: 'button', class: 'pfe-primary', text: '저장하고 보기', disabled: true, onclick: async () => {
      save.disabled = true;
      try { await api('theme', { vars: changed }); reload('테마를 저장했습니다 (theme/theme.css)'); } catch (err) { save.disabled = false; fail(box, err); }
    } });
    let last = null;
    const rows = vars.map((v) => {
      const head = v.group !== last ? el('p', { class: 'pfe-h', text: (last = v.group) || '테마 값' }) : null;
      let input;
      if (v.name === '--t-scheme') input = el('select', { value: v.value, onchange: () => live(v, input.value) }, el('option', { value: 'light', text: 'light — 밝은 바탕' }), el('option', { value: 'dark', text: 'dark — 어두운 바탕' }));
      else if (hex.test(v.value)) {
        const t = el('input', { type: 'text', class: 'pfe-grow', value: v.value, oninput: () => { if (hex.test(t.value)) { c.value = t.value; live(v, t.value.toUpperCase()); } } });
        const c = el('input', { type: 'color', value: v.value, oninput: () => { t.value = c.value.toUpperCase(); live(v, t.value); } });
        input = el('div', { class: 'pfe-line' }, c, t);
      } else input = el('input', { type: 'text', value: v.value, onchange: () => input.value.trim() && live(v, input.value.trim()) });
      return [head, el('div', { class: 'pfe-row' }, el('span', { class: 'pfe-l' }, v.note || v.name, ' ', el('span', { class: 'pfe-key', text: v.name })), input)];
    });
    add(box, el('p', { class: 'pfe-hint', text: '색·모양 값을 바꾸면 이 화면에 바로 보입니다. [저장하고 보기]를 눌러야 파일에 남고, 세 페이지가 함께 바뀝니다. 글자가 잘 읽히는지(대비)는 직접 확인해 주세요.' }),
      rows, el('div', { class: 'pfe-acts' }, save, el('button', { type: 'button', text: '바꾸기 전으로', onclick: () => reload() })));
  }

  // ── 탭: 바뀐 파일(git) ──
  async function tabFiles(box) {
    const ch = await api('changes');
    if (!ch.git) { add(box, el('p', { text: 'git 상태를 읽지 못했습니다.' }), el('pre', { text: ch.note || '' })); return; }
    const NAME = { modified: '고침', new: '새 파일', deleted: '지움' };
    add(box, el('p', { class: 'pfe-hint', text: '마지막 커밋 뒤에 바뀐 내 파일입니다. [되돌리기]는 그 파일을 마지막 커밋 상태로 돌립니다(되돌린 것은 다시 살릴 수 없습니다). 커밋·올리기는 평소처럼 git에서 합니다.' }),
      ch.files.length ? ch.files.map((f) => el('div', { class: 'pfe-card pfe-line' }, el('span', { class: 'pfe-tag', text: NAME[f.state] }), el('span', { class: 'pfe-grow pfe-key', text: f.path }),
        f.state === 'modified' && el('button', { type: 'button', class: 'pfe-mini pfe-danger', text: '되돌리기', onclick: async () => {
          if (!confirm(`${f.path}\n\n이 파일을 마지막 커밋 상태로 되돌릴까요? 그 뒤에 고친 내용은 사라집니다.`)) return;
          try { await api('revert', { path: f.path }); reload(`${f.path}을(를) 되돌렸습니다`); } catch (err) { fail(box, err); }
        } }))) : el('p', { text: '바뀐 파일이 없습니다.' }));
  }

  // ── 탭: 문서(상세 기술문서 페이지) ──
  const currentHeading = () => { const a = document.querySelector('#toc-links a.active'); try { return a ? decodeURIComponent(a.hash.slice(1)) : ''; } catch { return ''; } };
  async function tabDoc(box) {
    add(box, el('p', { text: '기술 문서는 글과 미리보기를 나란히 놓고 고칩니다.' }),
      el('div', { class: 'pfe-acts' }, el('button', { type: 'button', class: 'pfe-primary', text: '✎ 이 문서 고치기', onclick: () => { location.href = docEditUrl(pid, currentHeading()); } })),
      el('p', { class: 'pfe-hint', style: 'margin-top:12px', text: '지금 읽던 절에서 열립니다. 고친 글은 src/ 폴더의 기술 문서(.md)에 저장됩니다.' }));
  }

  // ── 탭: 장면(발표 덱) ──
  const LAYOUT = { overview: '개요 — 글 + 그림', visual: '그림 + 번호 카드', code: '코드 + 설명', full: '큰 그림·표 한 장 가득', screens: '화면 2~3열', problem: '문제 해결(문제 → 원인 → 해결 → 결과)' };
  const TABNAMES = ['', '개요', '구조', '흐름', '구현', '선택', '문제 해결', '결과'];
  const KIND = { image: '그림', code: '코드', table: '표', mermaid: '다이어그램' };
  const AUTO = [[/^cover$/, '표지'], [/^toc$/, '목차'], [/^ch-/, '장 표지'], [/^retro/, '결과와 회고'], [/^end$/, '맺음']];
  function sceneId() {
    let h = location.hash.slice(1);
    try { h = decodeURIComponent(h); } catch { /* 그대로 */ }
    const m = /^s=(.+)$/.exec(h);
    if (m) return m[1];
    try { const r = PFScene.report(); return r.ids[(r.cur || 1) - 1] || ''; } catch { return ''; }
  }
  async function tabScene(box) {
    if (!pid) { add(box, el('p', { text: '주소에 프로젝트(?p=폴더 이름)가 없습니다.' })); return; }
    const data = await (await fetch(`data/projects/${encodeURIComponent(pid)}.json`, { cache: 'no-store' })).json();
    const deck = data.deck;
    if (!deck?.slides?.length) { add(box, el('p', { text: '이 덱은 예전 데이터로 자동 구성되어 장면을 고칠 수 없습니다. Claude Code에서 /portfolio-build를 다시 실행해 주세요.' })); return; }
    const md = await (await fetch(PF.srcPath(pid, data.doc), { cache: 'no-store' })).text();
    const P = PF.md.parse(md, pid);
    const index = PF.md.index(P);
    const op = async (body, msg) => {
      box.querySelectorAll('button').forEach((b) => { b.disabled = true; });
      try { await api('deck', { id: pid, ...body }); reload(msg); }
      catch (err) { box.querySelectorAll('button').forEach((b) => { b.disabled = false; }); fail(box, err); }
    };
    const hidden = (deck.base || []).filter((b) => !deck.slides.some((s) => s.key === b.key));
    const keyOf = (sid) => { const k = sid.replace(/^d-/, ''); return deck.slides.some((s) => s.key === k) ? k : k.replace(/-(\d+|t\d+)$/, ''); };
    let curSid = null, dirty = false;
    const form = el('div');
    const extra = el('div', {},
      hidden.length ? [el('p', { class: 'pfe-h', text: `숨긴 장면 ${hidden.length}개` }), hidden.map((h) => el('div', { class: 'pfe-card pfe-line' },
        el('span', { class: 'pfe-grow pfe-ell', text: h.title?.text || h.key }), el('button', { type: 'button', class: 'pfe-mini', text: '다시 보이기', onclick: () => op({ op: 'undo', key: h.key }, '장면을 다시 보이게 했습니다') })))] : null,
      deck.slides.some((s) => s.edited) || hidden.length ? el('div', { class: 'pfe-acts' }, el('button', { type: 'button', class: 'pfe-danger', text: '이 덱에서 고친 것 모두 되돌리기', onclick: () => {
        if (confirm('이 덱의 장면 고치기(틀·글·순서·숨기기)를 모두 지우고 빌드한 그대로 되돌릴까요?')) op({ op: 'undo-all' }, '장면 고치기를 모두 되돌렸습니다');
      } })) : null);
    add(box, form, extra);
    const draw = () => {
      const sid = curSid;
      dirty = false;
      const slide = sid.startsWith('d-') ? deck.slides.find((s) => s.key === keyOf(sid)) : null;
      if (!slide) {
        const auto = AUTO.find(([re]) => re.test(sid));
        fill(form, el('p', { class: 'pfe-h', text: auto ? `${auto[1]} 장면` : '장면' }),
          el('p', { text: auto ? '이 장면은 페이지가 자동으로 만듭니다. 고칠 수 있는 것은 그 사이의 내용 장면입니다 — 화살표 키로 장면을 넘기면 여기에 그 장면이 나옵니다.' : '이 장면은 고칠 수 없습니다.' }),
          el('p', { class: 'pfe-hint', text: '표지 = 프로젝트 소개, 목차·장 표지 = 장면 목록, 결과와 회고 = 기술 문서의 회고 절에서 옵니다. 제목·기간 같은 값은 기술 문서를 고친 뒤 /portfolio-build.' }));
        return;
      }
      const w = JSON.parse(JSON.stringify(slide));
      w.points = w.points || []; w.visuals = w.visuals || [];
      const touch = () => { dirty = true; save.disabled = false; };
      const here = PF.md.findSection(P, w.sectionPath);
      const h0 = here.length === 1 ? here[0] : null;
      const sib = deck.slides.filter((s) => s.chapter === slide.chapter);
      const at = sib.indexOf(slide);
      const sel = (opts, value, on) => el('select', { value, onchange: on }, opts.map(([v, t]) => el('option', { value: v, text: t })));
      const fLayout = sel(Object.entries(LAYOUT), w.layout, () => { w.layout = fLayout.value; touch(); });
      const fTab = sel(TABNAMES.map((t) => [t, t || '(없음)']), w.tab || '', () => { w.tab = fTab.value; touch(); });
      const fTitle = el('input', { type: 'text', value: w.title?.text || '', oninput: touch });
      const fLead = el('textarea', { rows: 2, value: w.lead?.text || '', oninput: touch });
      const pBox = el('div'), vBox = el('div'), addBox = el('div');
      const move = (arr, i, d, redraw) => { arr.splice(i + d, 0, arr.splice(i, 1)[0]); touch(); redraw(); };
      const drawPoints = () => fill(pBox, ...w.points.map((p, i) => el('div', { class: 'pfe-card' },
        el('div', { class: 'pfe-line' }, el('input', { type: 'text', class: 'pfe-grow', placeholder: '라벨(없어도 됨)', value: p.label || '', oninput: (e) => { p.label = e.target.value; touch(); } }),
          el('button', { type: 'button', class: 'pfe-mini', text: '↑', disabled: i === 0, onclick: () => move(w.points, i, -1, drawPoints) }),
          el('button', { type: 'button', class: 'pfe-mini', text: '↓', disabled: i === w.points.length - 1, onclick: () => move(w.points, i, 1, drawPoints) }),
          el('button', { type: 'button', class: 'pfe-mini pfe-danger', text: '빼기', onclick: () => { w.points.splice(i, 1); touch(); drawPoints(); } })),
        el('textarea', { rows: 2, value: p.text || '', oninput: (e) => { p.text = e.target.value; touch(); } }))),
        w.points.length < 4 && el('button', { type: 'button', class: 'pfe-mini', text: '+ 요점 더하기', onclick: () => { w.points.push({ label: '', text: '' }); touch(); drawPoints(); } }));
      const lineCount = (v) => { const r = PF.md.pick(P, v, w.sectionPath); return r.el ? r.el.textContent.replace(/\n$/, '').split('\n').length : 0; };
      const describe = (v) => (v.kind === 'image' ? v.src : `${v.n || 1}번째 · ${v.fp || ''}`);
      const thumb = (v) => (v.kind === 'image' && !/\.(mp4|webm)$/i.test(v.src) ? el('img', { class: 'pfe-thumb', src: PF.srcPath(pid, PF.md.decode(v.src)), alt: '', loading: 'lazy' }) : null);
      const drawVisuals = () => fill(vBox, ...w.visuals.map((v, i) => {
        const total = v.kind === 'code' ? lineCount(v) : 0;
        const a = el('input', { type: 'number', class: 'pfe-num', min: 1, max: total, value: v.lines?.[0] ?? 1, 'aria-label': '시작 줄' });
        const b = el('input', { type: 'number', class: 'pfe-num', min: 1, max: total, value: v.lines?.[1] ?? total, 'aria-label': '끝 줄' });
        const setLines = () => {
          const x = Math.max(1, Math.min(total, +a.value || 1)), y = Math.max(x, Math.min(total, +b.value || total));
          if (x === 1 && y === total) delete v.lines; else v.lines = [x, y];
          touch();
        };
        a.onchange = b.onchange = setLines;
        return el('div', { class: 'pfe-card' }, el('div', { class: 'pfe-line' }, thumb(v), el('span', { class: 'pfe-tag', text: KIND[v.kind] }), el('span', { class: 'pfe-grow pfe-ell', title: describe(v), text: describe(v) }),
          el('button', { type: 'button', class: 'pfe-mini', text: '↑', disabled: i === 0, onclick: () => move(w.visuals, i, -1, drawVisuals) }),
          el('button', { type: 'button', class: 'pfe-mini', text: '↓', disabled: i === w.visuals.length - 1, onclick: () => move(w.visuals, i, 1, drawVisuals) }),
          el('button', { type: 'button', class: 'pfe-mini pfe-danger', text: '빼기', onclick: () => { w.visuals.splice(i, 1); touch(); drawVisuals(); } })),
          v.sectionPath?.length && el('small', { text: `다른 절: ${v.sectionPath[v.sectionPath.length - 1]}` }),
          v.kind === 'code' && total > 0 && el('div', { class: 'pfe-line' }, el('small', { text: `보일 줄(전체 ${total}줄)` }), a, el('span', { text: '~' }), b));
      }), w.visuals.length < 3 && el('button', { type: 'button', class: 'pfe-mini', text: addBox.childNodes.length ? '원문 목록 닫기' : '+ 원문에서 그림·코드·표 고르기', onclick: () => { addBox.childNodes.length ? fill(addBox, ) : drawAdd(h0 || P.headings[0]); drawVisuals(); } }));
      const same = (v, c) => v.kind === c.kind && (v.kind === 'image' ? PF.md.decode(v.src) === PF.md.decode(c.src) : (v.n || 1) === c.n) && String(v.sectionPath || '') === String(c.sectionPath || '');
      const drawAdd = (h) => {
        const b = index.get(h) || {};
        const path = h === h0 ? null : [...h.parents.map((x) => x.text), h.text];
        const cands = [];
        ['image', 'mermaid', 'code', 'table'].forEach((kind) => (b[kind] || []).forEach((e, i) => {
          const c = { kind };
          if (kind === 'image') {
            c.src = e.dataset.mdSrc || e.getAttribute('src');
            const dup = b.image.filter((x) => (x.dataset.mdSrc || x.getAttribute('src')) === c.src);
            if (dup.length > 1) c.n = dup.indexOf(e) + 1;
            if (e.alt) c.caption = { text: e.alt, verbatim: true };
          } else { c.n = i + 1; c.fp = PF.md.fpOf(e, kind); }
          if (path) c.sectionPath = path;
          cands.push(c);
        }));
        const pickH = el('select', { value: String(P.headings.indexOf(h)), onchange: () => drawAdd(P.headings[+pickH.value]) },
          P.headings.map((x, i) => el('option', { value: String(i), text: `${'　'.repeat(Math.max(0, x.level - 2))}${x.text}${x === h0 ? '  (이 장면의 절)' : ''}` })));
        fill(addBox, el('div', { class: 'pfe-card' }, el('small', { text: '기술 문서의 어느 절에서 고를까요?' }), pickH,
          cands.length ? cands.map((c) => el('div', { class: 'pfe-line' }, thumb(c), el('span', { class: 'pfe-tag', text: KIND[c.kind] }), el('span', { class: 'pfe-grow pfe-ell', title: describe(c), text: describe(c) }),
            el('button', { type: 'button', class: 'pfe-mini', text: w.visuals.some((v) => same(v, c)) ? '들어 있음' : '더하기', disabled: w.visuals.some((v) => same(v, c)) || w.visuals.length >= 3,
              onclick: () => { w.visuals.push(c); touch(); drawAdd(h); drawVisuals(); } })))
            : el('small', { text: '이 절 바로 아래에는 그림·코드·표가 없습니다. 하위 절을 골라 보세요.' })));
      };
      const save = el('button', { type: 'button', class: 'pfe-primary', text: '저장하고 보기', disabled: true, onclick: () => op({ op: 'update', key: slide.key, slide: {
        layout: w.layout, tab: w.tab || '', title: { text: fTitle.value }, lead: { text: fLead.value }, points: w.points.map((p) => ({ label: p.label || '', text: p.text || '' })), visuals: w.visuals } }, '장면을 고쳤습니다') });
      fill(form, 
        el('div', { class: 'pfe-line' }, el('p', { class: 'pfe-h pfe-grow pfe-ell', style: 'margin:0', text: slide.title?.text || slide.key }), slide.edited && el('span', { class: 'pfe-tag', text: '고친 장면' })),
        el('p', { class: 'pfe-key', style: 'margin:2px 0 12px', text: `#s=${sid}  ·  ${(w.sectionPath || []).slice(-1)[0] || ''}` }),
        !h0 && el('p', { class: 'pfe-flash pfe-bad', text: '이 장면이 가리키는 절을 기술 문서에서 찾지 못했습니다(제목이 바뀌었을 수 있음). /portfolio-build를 다시 실행하면 맞춰집니다.' }),
        el('div', { class: 'pfe-acts', style: 'margin:0 0 14px' },
          el('button', { type: 'button', text: '← 앞으로', disabled: at <= 0, title: '같은 장 안에서 한 칸 앞으로', onclick: () => op({ op: 'up', key: slide.key }, '장면 순서를 바꿨습니다') }),
          el('button', { type: 'button', text: '뒤로 →', disabled: at === sib.length - 1, title: '같은 장 안에서 한 칸 뒤로', onclick: () => op({ op: 'down', key: slide.key }, '장면 순서를 바꿨습니다') }),
          el('button', { type: 'button', text: '숨기기', onclick: () => op({ op: 'hide', key: slide.key }, '장면을 숨겼습니다 (아래 "숨긴 장면"에서 다시 보이게 할 수 있습니다)') }),
          slide.edited && el('button', { type: 'button', class: 'pfe-danger', text: '고친 것 되돌리기', onclick: () => op({ op: 'undo', key: slide.key }, '이 장면을 빌드한 그대로 되돌렸습니다') })),
        el('label', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: '장면 틀' }), fLayout),
        el('label', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: '머리 오른쪽 라벨' }), fTab),
        el('label', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: '제목' }), fTitle),
        el('label', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: '도입(1~2문장)' }), fLead),
        el('div', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: `요점(4개까지)` }), pBox),
        el('div', { class: 'pfe-row' }, el('span', { class: 'pfe-l', text: '그림·코드·표(3개까지, 기술 문서 원문을 그대로 보여 줌)' }), vBox, addBox),
        el('div', { class: 'pfe-acts' }, save, el('button', { type: 'button', text: '바꾸기 전으로', onclick: draw })),
        el('p', { class: 'pfe-hint', style: 'margin-top:12px', text: '그림·코드·표의 내용은 기술 문서에서 고칩니다. 여기서 직접 쓴 글은 "직접 고친 글"로 표시되어 다음 점검 리포트의 "확인해 주세요"에 나옵니다 — 사실과 설명은 기술 문서에도 적어 두세요.' }));
      drawPoints(); drawVisuals();
    };
    const tick = () => {
      if (!box.isConnected) return clearInterval(timer);
      const sid = sceneId();
      if (sid && sid !== curSid && !dirty) { curSid = sid; draw(); }
    };
    curSid = sceneId() || 'cover';
    draw();
    const timer = setInterval(tick, 300);
  }

  // ── 화면에서 글 고르기: 누른 글을 어디서 고치는지 찾아 줌 ──
  const toast = (...kids) => {
    document.querySelectorAll('.pfe-toast').forEach((t) => t.remove());
    const t = el('div', { class: 'pfe pfe-toast', 'data-pf-ui': 'edit', role: 'status' }, kids);
    document.body.append(t);
    setTimeout(() => t.remove(), 9000);
  };
  function pick() {
    const html = document.documentElement;
    let hover = null;
    const clean = (n) => { const c = n.cloneNode(true); c.querySelectorAll('.pf-sr-only, .pf-newtab-sr, svg').forEach((x) => x.remove()); return c.textContent.replace(/\s+/g, ' ').trim(); };
    const over = (e) => { hover?.classList.remove('pfe-pick-hover'); hover = root.contains(e.target) ? null : e.target; hover?.classList.add('pfe-pick-hover'); };
    const stop = () => { html.classList.remove('pfe-picking'); hover?.classList.remove('pfe-pick-hover'); removeEventListener('mouseover', over, true); removeEventListener('click', click, true); removeEventListener('keydown', esc, true); };
    const esc = (e) => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); stop(); } };
    const click = (e) => {
      if (root.contains(e.target)) return stop();
      e.preventDefault(); e.stopPropagation(); stop();
      const t = e.target;
      // ① 문구 표의 문구인가(누른 요소와 위 두 단계까지)
      if (S) {
        const table = S.keys().map((k) => [k, S.t(k).replace(/\s+/g, ' ').trim()]).filter(([, v]) => v && !v.includes('{'));
        for (let n = t, i = 0; n && n !== document.body && i < 3; n = n.parentElement, i++) {
          const txt = clean(n);
          const hit = txt && table.find(([, v]) => v === txt);
          if (hit) { state.q = hit[0]; toggle(true); show('str'); return; }
        }
      }
      // ② 내 정보
      if (PAGE === 'portfolio' && t.closest('#cover h1, #cover .headline, #cover .btns, #intro .about, #contact .ct-me, .top-name')) { toggle(true); show('me'); return; }
      // ③ 발표 덱 장면
      if (PAGE === 'deck' && t.closest('.pf-scene')) { toggle(true); show('scene'); return; }
      // ④ 기술 문서에서 온 글
      const sec = t.closest('section[id^="p-"]');
      const proj = PAGE === 'doc' ? pid : sec ? sec.id.replace(/^p-/, '').replace(/-core$/, '') : '';
      if (proj) {
        const inDoc = PAGE === 'doc' && t.closest('#doc');
        const h = inDoc ? (() => { let n = t; while (n && n.parentElement?.id !== 'doc') n = n.parentElement; while (n && !/^H[1-6]$/.test(n.tagName)) n = n.previousElementSibling; return n?.id || ''; })() : '';
        return toast(el('span', { text: inDoc ? '이 글은 기술 문서 본문입니다. ' : '이 글은 기술 문서에서 뽑은 요약입니다. 기술 문서를 고친 뒤 /portfolio-build를 다시 실행하면 바뀝니다. ' }),
          el('a', { href: docEditUrl(proj, h), text: '기술 문서 고치기 →' }));
      }
      toast(el('span', { text: '이 글을 어디서 고치는지 찾지 못했습니다. [내 정보]·[문구] 탭이나 기술 문서를 확인해 주세요.' }));
    };
    html.classList.add('pfe-picking');
    addEventListener('mouseover', over, true); addEventListener('click', click, true); addEventListener('keydown', esc, true);
    toast(el('span', { text: '고치고 싶은 글을 누르세요. (Esc = 그만두기)' }));
  }

  // ── 상세 문서 미리보기 틀(editor/doc-edit.html의 오른쪽) ──
  //   고친 글(아직 저장하지 않은 것)을 기술 문서 대신 읽게 하고, 문서 그리기(assets/doc.js)를 다시 돌려 화면을 바꿉니다.
  //   새 화면은 보이지 않는 곳에서 다 그린 뒤 한 번에 바꿔 끼워 깜박이지 않습니다.
  function frameDoc() {
    const html = document.documentElement;
    html.classList.add('pfe-frame');
    const realFetch = window.fetch.bind(window);
    const winAdd = window.addEventListener.bind(window);
    let draft = null, mdPath = '', listeners = [], running = null, queued = null;
    const hooks = { onPick: null, onRendered: null };
    const pathOf = (u) => { try { return decodeURIComponent(new URL(String(u), location.href).pathname); } catch { return ''; } };
    window.fetch = (input, init) => (draft != null && mdPath && typeof input === 'string' && pathOf(input) === mdPath
      ? Promise.resolve(new Response(draft, { status: 200, headers: { 'Content-Type': 'text/markdown; charset=utf-8' } })) : realFetch(input, init));
    window.addEventListener = (type, fn, opt) => { listeners.push([type, fn, opt]); return winAdd(type, fn, opt); };
    const blocks = () => { const d = document.getElementById('doc'); return d ? [...d.children].filter((c) => !c.matches('.link-list')) : []; };

    async function once(text) {
      draft = text;
      try { await window.PFDoc.whenReady(); } catch { /* */ }
      const old = document.getElementById('app');
      listeners.forEach(([t, f, o]) => window.removeEventListener(t, f, o)); listeners = [];
      document.querySelectorAll('dialog.lightbox').forEach((d) => d.remove());
      if (location.hash) history.replaceState(null, '', location.pathname + location.search);
      const neo = document.createElement('div');
      neo.id = 'app';
      neo.style.cssText = 'position:absolute;left:0;right:0;top:0;visibility:hidden';
      old.id = 'app-old';
      old.before(neo);
      await new Promise((res) => { const s = document.createElement('script'); s.src = 'assets/doc.js'; s.onload = s.onerror = res; document.body.appendChild(s); });
      try { await window.PFDoc.whenReady(); } catch { /* */ }
      const y = scrollY;
      old.remove();
      neo.style.cssText = '';
      scrollTo({ top: y, behavior: 'instant' });
    }
    function render(text) {                                             // 그리는 중에 또 고치면 마지막 글만 한 번 더
      if (running) { queued = text; return running; }
      running = once(text).catch((e) => console.error('[edit] 미리보기를 다시 그리지 못함:', e)).then(() => {
        running = null;
        if (queued != null) { const t = queued; queued = null; return render(t); }
        hooks.onRendered?.();
      });
      return running;
    }
    function highlight(i, scroll) {
      const list = blocks();
      document.querySelectorAll('#doc > .pfe-cur').forEach((x) => x.classList.remove('pfe-cur'));
      const b = list[i];
      if (!b) return;
      b.classList.add('pfe-cur');
      const r = b.getBoundingClientRect();
      if (scroll && (r.top < 80 || r.top > innerHeight - 80)) scrollTo({ top: scrollY + r.top - innerHeight * 0.3, behavior: 'instant' });
    }
    document.addEventListener('click', (e) => {
      const d = document.getElementById('doc');
      if (!d || !d.contains(e.target) || e.target === d) return;
      let top = e.target;
      while (top.parentElement && top.parentElement !== d) top = top.parentElement;
      const i = blocks().indexOf(top);
      if (i < 0) return;
      if (e.target.closest('img, .mmd-zoom')) { e.stopPropagation(); e.preventDefault(); }       // 편집 중에는 크게 보기 대신 글 위치로
      const sub = {};
      const tr = e.target.closest('tr'), li = e.target.closest('li'), pre = e.target.closest('pre');
      if (tr && top.contains(tr)) sub.row = [...top.querySelectorAll('tr')].indexOf(tr);
      else if (li && top.contains(li)) sub.item = [...top.querySelectorAll('li')].indexOf(li);
      else if (pre && top.contains(pre) && !pre.closest('.mmd')) {
        const cs = getComputedStyle(pre.querySelector('code') || pre);
        const lh = parseFloat(cs.lineHeight) || parseFloat(cs.fontSize) * 1.5;
        sub.codeLine = Math.max(0, Math.floor((e.clientY - pre.getBoundingClientRect().top - parseFloat(getComputedStyle(pre).paddingTop)) / lh));
      }
      hooks.onPick?.(i, sub);
    }, true);
    window.PFEditFrame = { hooks, render, highlight, count: () => blocks().length, setDoc: (p) => { mdPath = p; },
      headingBlock: (id) => { const h = document.getElementById(id); return h ? blocks().indexOf(h) : -1; } };
  }
})();
