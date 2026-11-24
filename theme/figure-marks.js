/* 그림 표시 (theme/figure-marks.js) — 상세 문서(doc.html)·슬라이드(project.html)가 md-blocks.js 다음에 싣습니다. (theme/RENDERER-CHANGES.md 5)
   프로젝트 문서에서 그림 바로 뒤에 주석 한 줄을 두면, 그림의 강조할 곳에 테두리를 그리고 그 페이지의 글과 짝을 짓습니다.
   원칙: 글이 설명하는 곳만 표시합니다. 표시마다 짝이 될 글이 있어야 합니다.

   ① 낱말 짝(기본) — 표시 이름이 그 페이지 글 속 낱말입니다. 테두리와 그 낱말에 같은 색을 칠합니다(번호 없음).
     ![UserGameDataSO 인스펙터 — 레벨 데이터 · … · 해금 목록 · 편성 슬롯](images/user_data_so.jpg)
     <!-- pf-marks "레벨 데이터|레벨":0.5,15,99,12.5 "해금 목록|해금":0.5,67.5,99,12.5 -->
     - 낱말은 |로 여러 개(앞에서부터 찾음). 공백이 있으면 "…"로 감쌉니다.
     - 찾는 곳: 상세 문서는 그림이 있는 절의 글 → 그림 캡션, 슬라이드는 그 장면의 글(카드·도입) → 캡션. 처음 나온 한 곳만 칠합니다.
   ② 번호 짝 — 표시 이름이 숫자면 그림 바로 다음 번호 목록의 n번째 항목과 짝입니다(번호 배지).
     <!-- pf-marks 1:13.5,66,85,3.4 2:13.5,92.1,85,3.4 -->
     1. **Next Wave Index = 3** — …
     슬라이드에서는 장면의 번호 카드 수가 표시 수와 같으면 카드 번호를 표시 색으로 보이고, 다르면 캡션 끝에 "① 이름 ② 이름"을 붙입니다.
   - 좌표는 그림 크기에 대한 %: 이름:왼쪽,위,너비,높이. GitHub에서는 주석이 안 보입니다.
   - 둘 중 하나(테두리·낱말·목록 항목)에 마우스를 올리면 짝이 함께 강조됩니다.
   - 좌표 따기: doc.html?p=<폴더>&pf-mark=1 → 그림 위를 끌어서 상자를 그리면 주석 한 줄이 만들어집니다([복사]).
   - 점검: PFMarks.report() — issues(no-list · count · range · orphan · dup · word-missing: 상세 문서에서 짝 낱말을 못 찾음)
   글을 만들지 않습니다. 표시 좌표·낱말·목록은 모두 프로젝트 문서에 있는 그대로입니다. */
(() => {
  if (typeof PF === 'undefined' || !PF.md) return;   // PF는 common.js의 전역 const(window 속성이 아님)
  const RE_COMMENT =/^\s*pf-marks\b([\s\S]*)$/;
  const RE_MARK = /(?:"([^"]+)"|([^\s:"]+))\s*:\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)/g;
  const PALETTE = ['#ff5a36', '#2f9bff', '#1fbf75', '#f5b400', '#c45cff'];
  const isDeck = () => !!document.getElementById('deck');
  const circled = (n) => (n >= 1 && n <= 20 ? String.fromCharCode(0x2460 + n - 1) : `(${n})`);
  const bySrc = new Map();          // 그림 주소(해석된 src) → { gid, marks }
  const groups = [];                // 점검용
  let gidSeq = 0;

  // ── 1. 원문 해석: 주석 → 그림의 표시 + 다음 목록 ──
  function annotate(frag) {
    const w = document.createTreeWalker(frag, NodeFilter.SHOW_COMMENT);
    const comments = [];
    for (let c = w.nextNode(); c; c = w.nextNode()) if (RE_COMMENT.test(c.data)) comments.push(c);
    for (const c of comments) {
      const body = RE_COMMENT.exec(c.data)[1];
      let wi = 0;
      const marks = [...body.matchAll(RE_MARK)].map((m) => {
        const name = (m[1] || m[2]).trim(), num = /^\d+$/.test(name) ? +name : null;
        const mk = { key: num != null ? String(num) : name, n: num, x: +m[3], y: +m[4], w: +m[5], h: +m[6] };
        if (num == null) { mk.words = name.split('|').map((s) => s.trim()).filter(Boolean); mk.color = PALETTE[wi++ % PALETTE.length]; }
        return mk;
      });
      const numbered = marks.filter((m) => m.n != null);
      // 같은 문단 안(그림 줄 바로 아래) 또는 빈 줄 뒤의 블록
      let img = null, para = null;
      if (c.parentNode && c.parentNode.nodeType === 1 && c.parentNode.tagName === 'P') {
        para = c.parentNode;
        let n = c.previousSibling;
        while (n && !img) { if (n.nodeType === 1) img = n.tagName === 'IMG' ? n : n.querySelector?.('img:last-of-type') || null; n = n.previousSibling; }
      } else {
        let prev = c.previousSibling;
        while (prev && prev.nodeType !== 1) prev = prev.previousSibling;
        if (prev) { para = prev; img = prev.tagName === 'IMG' ? prev : [...prev.querySelectorAll('img')].pop() || null; }
      }
      const next = c.nextSibling;
      if (next && next.nodeType === 3 && !next.textContent.trim()) next.remove();
      c.remove();
      const g = { gid: ++gidSeq, src: img ? (img.dataset.mdSrc || img.getAttribute('src')) : null, marks: marks.map((m) => m.key), items: 0, issues: [] };
      groups.push(g);
      if (!img) { g.issues.push({ type: 'orphan', detail: '주석 앞에 그림이 없습니다' }); continue; }
      const seen = new Set();
      marks.forEach((m) => {
        if (seen.has(m.key)) g.issues.push({ type: 'dup', detail: `"${m.key}"가 두 번 있습니다` });
        seen.add(m.key);
        if (m.w <= 0 || m.h <= 0 || m.x < 0 || m.y < 0 || m.x + m.w > 100.5 || m.y + m.h > 100.5) g.issues.push({ type: 'range', detail: `"${m.key}" 좌표가 그림 밖입니다 (${m.x},${m.y},${m.w},${m.h})` });
      });
      // 번호 짝: 바로 다음 번호 목록
      let list = para ? para.nextSibling : null;
      while (list && list.nodeType !== 1) list = list.nextSibling;
      const labels = new Map();
      if (!numbered.length) { /* 낱말 짝만 — 목록이 필요 없음 */ }
      else if (list && list.tagName === 'OL') {
        list.classList.add('pf-mk-list');
        list.dataset.pfMkGroup = g.gid;
        const lis = [...list.children].filter((x) => x.tagName === 'LI');
        g.items = lis.length;
        lis.forEach((li, i) => {
          li.dataset.pfMk = `${g.gid}:${i + 1}`;
          li.dataset.pfMkN = i + 1;
          const key = li.querySelector('strong, code');
          labels.set(i + 1, (key ? key.textContent : li.textContent).trim().split(/\s+—|\s+-\s/)[0].slice(0, 30));
        });
        if (g.items !== numbered.length) g.issues.push({ type: 'count', detail: `번호 표시 ${numbered.length}개 · 목록 ${g.items}개` });
      } else {
        g.issues.push({ type: 'no-list', detail: '번호 표시가 있는데 그림 뒤에 번호 목록(1. 2. …)이 없습니다' });
      }
      numbered.forEach((m) => { m.label = labels.get(m.n) || ''; });
      img.dataset.pfMarks = g.gid;
      const legend = numbered.filter((m) => m.label).map((m) => `${circled(m.n)} ${m.label}`).join('  ');
      bySrc.set(img.getAttribute('src'), { gid: g.gid, marks, numbered: numbered.length, legend, group: g });
      if (legend && img.getAttribute('alt')) legendByAlt.set(squash(img.getAttribute('alt')), legend);
    }
  }
  // 슬라이드 캡션에 짝 붙이기: 캡션 HTML을 만드는 PF.inline을 감쌈 → 장면 크기를 잴 때 짝 글이 이미 들어 있음
  //   (project-deck.js가 실릴 때 PF.inline을 받아 두므로, 이 파일이 그보다 먼저 실려야 함). 캡션 글이 그림의 대체 글과 같을 때만.
  const squash = (s) => String(s || '').replace(/\s+/g, ' ').trim();
  const legendByAlt = new Map();
  if (isDeck() && typeof PF.inline === 'function') {
    const origInline = PF.inline;
    PF.inline = function (t, ...rest) {
      const out = origInline.call(this, t, ...rest);
      const lg = typeof t === 'string' && legendByAlt.get(squash(t));
      return lg ? `${out}<span class="pf-mk-legend">${PF.esc(lg)}</span>` : out;
    };
  }
  const origParse = PF.md.parse;
  PF.md.parse = function (md, id) { const r = origParse.call(this, md, id); try { annotate(r.frag); } catch (e) { console.warn('[figure-marks]', e); } return r; };

  // ── 2. 그리기: 그림의 실제 내용 영역(object-fit 포함)에 맞춘 덮개 ──
  function hostOf(img) {
    let h = img.parentElement;
    while (h && h.parentElement && getComputedStyle(h).display.startsWith('inline')) h = h.parentElement;
    if (h && getComputedStyle(h).position === 'static') h.classList.add('pf-mk-host');
    return h;
  }
  function contentRect(img, ov) {
    const cw = img.clientWidth, ch = img.clientHeight;
    const nw = img.naturalWidth || +ov.dataset.nw || 0, nh = img.naturalHeight || +ov.dataset.nh || 0;
    if (!cw || !ch || !nw || !nh) return null;
    ov.dataset.nw = nw; ov.dataset.nh = nh;
    const fit = getComputedStyle(img).objectFit;
    let w = cw, h = ch, x = 0, y = 0;
    if (fit === 'contain' || fit === 'scale-down') {
      let s = Math.min(cw / nw, ch / nh);
      if (fit === 'scale-down') s = Math.min(s, 1);
      w = nw * s; h = nh * s; x = (cw - w) / 2; y = (ch - h) / 2;
    }
    return { x: x + img.clientLeft, y: y + img.clientTop, w, h };
  }
  function layout(img, ov) {
    const host = ov.parentElement;
    const r = contentRect(img, ov);
    if (!host || !r) return;
    let ox = 0, oy = 0, el = img;
    while (el && el !== host) { ox += el.offsetLeft; oy += el.offsetTop; el = el.offsetParent; }
    if (el !== host) { const a = img.getBoundingClientRect(), b = host.getBoundingClientRect(), k = host.offsetWidth ? b.width / host.offsetWidth : 1; ox = (a.left - b.left) / k - host.clientLeft; oy = (a.top - b.top) / k - host.clientTop; }
    Object.assign(ov.style, { left: `${ox + r.x}px`, top: `${oy + r.y}px`, width: `${r.w}px`, height: `${r.h}px` });
  }
  const ro = 'ResizeObserver' in window ? new ResizeObserver((es) => es.forEach((e) => { const ov = e.target._pfMk; if (ov) layout(e.target, ov); })) : null;
  function mount(img) {
    const info = bySrc.get(img.getAttribute('src'));
    if (!info) return;
    let ov = img._pfMk;
    if (!ov) {
      const sib = img.parentElement && [...img.parentElement.querySelectorAll(':scope > .pf-mk')].find((o) => o.dataset.for === img.getAttribute('src'));
      ov = sib || null;
    }
    if (!ov) {
      const host = hostOf(img);
      if (!host) return;
      ov = document.createElement('div');
      ov.className = 'pf-mk';
      ov.setAttribute('aria-hidden', 'true');
      ov.dataset.for = img.getAttribute('src');
      ov.innerHTML = info.marks.map((m) => {
        const pos = `left:${m.x}%;top:${m.y}%;width:${m.w}%;height:${m.h}%`;
        if (m.words) return `<div class="pf-mk-box w" data-pf-mk="${info.gid}:${PF.esc(m.key)}" style="${pos};--c:${m.color}"></div>`;
        const edge = `${m.x < 3 ? ' ex' : ''}${m.y < 3 ? ' ey' : ''}`;
        return `<div class="pf-mk-box${edge}" data-pf-mk="${info.gid}:${m.n}" style="${pos}"><span class="pf-mk-n">${m.n}</span></div>`;
      }).join('');
      host.appendChild(ov);
    }
    img._pfMk = ov;
    // 슬라이드: 장면의 번호 카드가 표시와 같은 수면 카드 번호가 짝 → 캡션의 짝 글을 숨김.
    //   다르면 카드 번호를 다시 숨김(표시 번호와 헷갈리지 않게). 둘 다 숨기기만 하므로 잰 뒤에 해도 넘치지 않음
    const fig = img.closest('.vbox');
    const scene = fig && fig.closest('.pf-scene');
    if (scene && info.numbered) {
      const nums = scene.querySelectorAll('.ncard > .num').length;
      if (nums === info.numbered) fig.querySelectorAll('.pf-mk-legend').forEach((e) => e.classList.add('pf-mk-dup'));
      else if (nums) scene.classList.add('pf-mk-nonum');
    }
    linkWords(img, info);
    layout(img, ov);
    if (!img._pfMkWatch) {
      img._pfMkWatch = true;
      img.addEventListener('load', () => layout(img, ov));
      if (ro) ro.observe(img);
    }
  }
  // 낱말 짝: 그 페이지 글 속 낱말을 표시와 같은 색으로 칠함(감싸기만 하므로 글자 크기·줄 수가 바뀌지 않음)
  function textScopes(img) {
    const scene = img.closest('.pf-scene');
    if (scene) {                                                           // 슬라이드: 그 장면의 글 → 캡션
      const fig = img.closest('.vbox');
      const body = [...scene.querySelectorAll('.ncard, .fstep, .lead, .pf-lead, .dk-text p, .rt p')].filter((e) => !e.closest('.vbox'));
      return [body, fig ? [...fig.querySelectorAll('figcaption')] : []];
    }
    let para = img.closest('p, figure') || img.parentElement;             // 상세 문서: 그림이 있는 절의 글 → 캡션
    while (para.parentElement && para.parentElement.id !== 'doc' && para.parentElement !== document.body) para = para.parentElement;
    const isHead = (e) => /^H[1-6]$/.test(e.tagName);
    const before = [], after = [];
    for (let e = para.previousElementSibling; e && !isHead(e); e = e.previousElementSibling) before.unshift(e);
    for (let e = para.nextElementSibling; e && !isHead(e); e = e.nextElementSibling) after.push(e);
    const body = [...before, ...after].filter((e) => e.tagName !== 'PRE' && !e.classList.contains('pf-figure') && !e.classList.contains('mmd'));
    return [body, [...para.querySelectorAll('.pf-cap, figcaption')]];
  }
  function findText(els, word) {
    for (const el of els) {
      const w = document.createTreeWalker(el, NodeFilter.SHOW_TEXT, { acceptNode: (n) => (n.parentElement.closest('pre, mark.pf-mk-word, .pf-mk, .pf-mk-legend') ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT) });
      for (let n = w.nextNode(); n; n = w.nextNode()) { const i = n.data.indexOf(word); if (i >= 0) return { n, i }; }
    }
    return null;
  }
  function linkWords(img, info) {
    const words = info.marks.filter((m) => m.words);
    if (!words.length) return;
    const scopes = textScopes(img);
    const all = scopes.flat();
    words.forEach((m) => {
      const key = `${info.gid}:${m.key}`;
      if (all.some((el) => el.querySelector(`mark.pf-mk-word[data-pf-mk="${CSS.escape(key)}"]`))) return;   // 이미 칠함(복제본 등)
      let hit = null;
      for (const els of scopes) { for (const word of m.words) { hit = findText(els, word); if (hit) { hit.len = word.length; break; } } if (hit) break; }
      if (!hit) {
        if (!img.closest('.pf-scene') && !info.group.issues.some((x) => x.type === 'word-missing' && x.key === m.key))
          info.group.issues.push({ type: 'word-missing', key: m.key, detail: `"${m.words.join('|')}"를 그림이 있는 절의 글·캡션에서 찾지 못했습니다` });
        return;
      }
      const r = document.createRange();
      r.setStart(hit.n, hit.i); r.setEnd(hit.n, hit.i + hit.len);
      const mk = document.createElement('mark');
      mk.className = 'pf-mk-word';
      mk.dataset.pfMk = key;
      mk.style.setProperty('--c', m.color);
      r.surroundContents(mk);
    });
  }
  function scan(root) {
    if (!bySrc.size || !root.querySelectorAll) return;
    if (root.tagName === 'IMG') { mount(root); return; }
    root.querySelectorAll('img').forEach((img) => { if (bySrc.has(img.getAttribute('src'))) mount(img); });
  }
  new MutationObserver((recs) => recs.forEach((r) => r.addedNodes.forEach((n) => { if (n.nodeType === 1) scan(n); })))
    .observe(document.documentElement, { childList: true, subtree: true });
  addEventListener('resize', () => document.querySelectorAll('img').forEach((img) => { if (img._pfMk) layout(img, img._pfMk); }));

  // ── 3. 짝 강조(상세 문서): 표시 ↔ 목록 항목 ──
  const flip = (key, on) => document.querySelectorAll(`[data-pf-mk="${CSS.escape(key)}"]`).forEach((e) => e.classList.toggle('on', on));
  document.addEventListener('mouseover', (e) => { const t = e.target.closest?.('[data-pf-mk]'); if (t) flip(t.dataset.pfMk, true); });
  document.addEventListener('mouseout', (e) => { const t = e.target.closest?.('[data-pf-mk]'); if (t && !t.contains(e.relatedTarget)) flip(t.dataset.pfMk, false); });
  document.addEventListener('click', (e) => {                                  // 표시를 눌러도 그림 크게 보기가 열리게
    const b = e.target.closest?.('.pf-mk-box');
    if (!b || markMode) return;
    const img = [...b.closest('.pf-mk').parentElement.querySelectorAll('img')].find((i) => i._pfMk === b.closest('.pf-mk'));
    if (img) img.click();
  });

  // ── 4. 좌표 따기(?pf-mark=1, 상세 문서) ──
  const markMode = new URLSearchParams(location.search).get('pf-mark') === '1' && !isDeck();
  if (markMode) {
    let target = null, start = null, temp = null, rects = [];
    const panel = document.createElement('div');
    panel.className = 'pf-mk-panel';
    panel.innerHTML = '<p class="t">좌표 따기 — 그림 위를 끌어서 상자를 그리세요</p><pre class="out">아직 없음</pre><p class="b"><button type="button" data-a="copy">복사</button> <button type="button" data-a="undo">하나 지우기</button> <button type="button" data-a="clear">모두 지우기</button></p>';
    const out = panel.querySelector('.out');
    const line = () => (rects.length ? `<!-- pf-marks ${rects.map((r, i) => `${i + 1}:${r.map((v) => +v.toFixed(1)).join(',')}`).join(' ')} -->` : '아직 없음');
    const redraw = () => {
      out.textContent = line();
      document.querySelectorAll('.pf-mk-draft').forEach((d) => d.remove());
      if (!target) return;
      const host = hostOf(target), ov = document.createElement('div');
      ov.className = 'pf-mk pf-mk-draft';
      ov.innerHTML = rects.map((r, i) => `<div class="pf-mk-box" style="left:${r[0]}%;top:${r[1]}%;width:${r[2]}%;height:${r[3]}%"><span class="pf-mk-n">${i + 1}</span></div>`).join('');
      host.appendChild(ov); layout(target, ov);
    };
    const pct = (img, e) => {
      const r = contentRect(img, img._pfMk || { dataset: {} }); const b = img.getBoundingClientRect(), k = img.offsetWidth ? b.width / img.offsetWidth : 1;
      const x = ((e.clientX - b.left) / k - r.x) / r.w * 100, y = ((e.clientY - b.top) / k - r.y) / r.h * 100;
      return [Math.max(0, Math.min(100, x)), Math.max(0, Math.min(100, y))];
    };
    document.addEventListener('mousedown', (e) => {
      const img = e.target.closest?.('#doc img') || (e.target.closest?.('.pf-mk') && [...e.target.closest('.pf-mk').parentElement.querySelectorAll('img')][0]);
      if (!img) return;
      e.preventDefault();
      if (img !== target) { target = img; rects = []; }
      start = pct(img, e);
      temp = [start[0], start[1], 0, 0];
      rects.push(temp);
    }, true);
    document.addEventListener('mousemove', (e) => {
      if (!start || !target) return;
      const p = pct(target, e);
      temp.splice(0, 4, Math.min(start[0], p[0]), Math.min(start[1], p[1]), Math.abs(p[0] - start[0]), Math.abs(p[1] - start[1]));
      redraw();
    });
    document.addEventListener('mouseup', () => { if (!start) return; start = null; if (temp && (temp[2] < 0.5 || temp[3] < 0.5)) rects.pop(); temp = null; redraw(); });
    document.addEventListener('click', (e) => { if (e.target.closest?.('#doc img, .pf-mk')) { e.preventDefault(); e.stopImmediatePropagation(); } }, true);
    panel.addEventListener('click', (e) => {
      const a = e.target.closest('button')?.dataset.a;
      if (a === 'copy' && rects.length) navigator.clipboard?.writeText(line()).then(() => { e.target.textContent = '복사함'; setTimeout(() => { e.target.textContent = '복사'; }, 1200); });
      if (a === 'undo') { rects.pop(); redraw(); }
      if (a === 'clear') { rects = []; redraw(); }
    });
    const add = () => document.body.appendChild(panel);
    if (document.body) add(); else addEventListener('DOMContentLoaded', add);
  }

  // ── 5. 모양 ──
  const css = `
  :root { --pf-mark: #ff5a36; --pf-mark-ink: #fff; }
  .pf-mk-host { position: relative; }
  .pf-mk { position: absolute; pointer-events: none; z-index: 2; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
  .pf-mk-box { position: absolute; box-sizing: border-box; border: 2px solid var(--pf-mark); border-radius: 4px; box-shadow: 0 0 0 1px rgba(0,0,0,.45); pointer-events: auto; cursor: zoom-in; transition: background-color .15s, box-shadow .15s; }
  .pf-mk-box.on { background: rgba(255, 90, 54, .2); box-shadow: 0 0 0 1px rgba(0,0,0,.45), 0 0 0 4px rgba(255, 90, 54, .35); }
  .pf-mk-n { position: absolute; left: 0; top: 0; transform: translate(-50%, -50%); min-width: 20px; height: 20px; padding: 0 4px; box-sizing: border-box; border-radius: 10px;
    background: var(--pf-mark); color: var(--pf-mark-ink); font: 700 12px/20px Pretendard, system-ui, sans-serif; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.5); }
  .pf-mk-box.ex .pf-mk-n { left: 2px; transform: translate(0, -50%); }
  .pf-mk-box.ey .pf-mk-n { top: 2px; transform: translate(-50%, 0); }
  .pf-mk-box.ex.ey .pf-mk-n { transform: none; }
  #doc .pf-mk-n, dialog .pf-mk-n { left: -3px; top: 50%; transform: translate(-100%, -50%); }   /* 상세 문서: 상자 왼쪽 바깥(이름 글자를 가리지 않게, 그림 밖으로 나가도 잘리지 않음) */
  .vbox .pf-mk-n { min-width: 26px; height: 26px; border-radius: 13px; font-size: 15px; line-height: 26px; }
  .vbox .pf-mk-box { border-width: 3px; }
  .pf-mk-box.w { border-color: var(--c); border-radius: 3px; }
  .pf-mk-box.w.on { background: color-mix(in srgb, var(--c) 22%, transparent); box-shadow: 0 0 0 1px rgba(0,0,0,.45), 0 0 0 4px color-mix(in srgb, var(--c) 40%, transparent); }
  mark.pf-mk-word { background: color-mix(in srgb, var(--c) 16%, transparent); color: inherit; border-bottom: 2px solid var(--c); border-radius: 2px; padding: 0; -webkit-box-decoration-break: clone; box-decoration-break: clone;
    -webkit-print-color-adjust: exact; print-color-adjust: exact; transition: background-color .15s; }
  mark.pf-mk-word.on { background: color-mix(in srgb, var(--c) 38%, transparent); }
  .pf-mk-legend { display: block; margin-top: 2px; font-weight: 600; color: var(--pf-mark); white-space: normal; }
  .pf-mk-legend.pf-mk-dup { display: none; }
  .pf-scene:has(.pf-mk-legend) .ncard > .num { display: flex; background: var(--pf-mark); color: var(--pf-mark-ink); }   /* 표시가 있는 장면: 카드 번호 = 그림 번호 */
  .pf-scene.pf-mk-nonum .ncard > .num { display: none; }
  ol.pf-mk-list { list-style: none; padding-left: 0; }
  ol.pf-mk-list > li { position: relative; padding: 2px 6px 2px 32px; margin-left: -6px; border-radius: 6px; transition: background-color .15s; }
  ol.pf-mk-list > li::before { content: attr(data-pf-mk-n); position: absolute; left: 6px; top: 4px; min-width: 20px; height: 20px; border-radius: 10px;
    background: var(--pf-mark); color: var(--pf-mark-ink); font: 700 12px/20px Pretendard, system-ui, sans-serif; text-align: center; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
  ol.pf-mk-list > li.on { background: rgba(255, 90, 54, .12); }
  .pf-mk-draft .pf-mk-box { border-style: dashed; }
  .pf-mk-panel { position: fixed; right: 16px; bottom: 16px; z-index: 1000; max-width: min(520px, calc(100vw - 32px)); padding: 12px 14px; border-radius: 10px;
    background: #1d1f24; color: #eee; font: 13px/1.5 Pretendard, system-ui, sans-serif; box-shadow: 0 6px 24px rgba(0,0,0,.35); }
  .pf-mk-panel .t { margin: 0 0 6px; font-weight: 700; }
  .pf-mk-panel .out { margin: 0 0 8px; padding: 8px; background: #000; border-radius: 6px; white-space: pre-wrap; word-break: break-all; font: 12px/1.5 D2Coding, Consolas, monospace; }
  .pf-mk-panel .b { margin: 0; }
  .pf-mk-panel button { font: inherit; padding: 3px 10px; border-radius: 6px; border: 1px solid #555; background: #2b2e35; color: #eee; cursor: pointer; }
  @media print { .pf-mk-panel { display: none; } }`;
  const st = document.createElement('style');
  st.id = 'pf-figure-marks';
  st.textContent = css;
  (document.head || document.documentElement).prepend(st);   // 앞에 둬서 theme.css 등이 --pf-mark를 덮어쓸 수 있게

  window.PFMarks = { report: () => ({ groups: groups.map((g) => ({ ...g })), issues: groups.reduce((a, g) => a + g.issues.length, 0) }), mount: scan };
})();
