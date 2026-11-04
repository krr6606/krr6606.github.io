// 발표 덱 → PowerPoint(.pptx) 내보내기 — project.html 아래 띠의 [PPTX로 저장] (단추는 assets/deck-kit.js가 그리고, 여기는 파일을 만드는 일만)
//   화면에 그린 장면을 그대로 잽니다. 장면마다 화면 밖에 배율 1로 복제해 두고 요소의 위치·크기·글자 모양을 읽어 PowerPoint 개체로 옮깁니다.
//   발표 덱의 배치(장면 틀·나누기·글자 크기 맞춤)를 다시 계산하지 않으므로 PDF와 같은 모습이 되고, 하나하나 고칠 수 있습니다.
//     글 → 글상자(글꼴·크기·색·굵기·자간·줄 간격·링크) · 카드·칩 → 도형(둥근 모서리·테두리. 글만 든 상자는 도형 안에 글)
//     표 → PowerPoint 표 · 코드 → 고정폭 글상자(구문 강조 색 그대로) · 그림 → 그림(원본 파일, GIF는 움직임 유지)
//     다이어그램(mermaid SVG) → SVG 그림(글자를 SVG 글자로 바꿔 넣음. PowerPoint 365에서 [도형으로 변환]하면 상자·글자를 하나씩 고칠 수 있음)
//     그러데이션(장면 배경·배지) → 그림(장면 배경은 슬라이드 배경). 그림자·흐림·회전 효과는 옮기지 않음
//   PDF와 같은 장면 집합(.pf-skip 제외) · 인쇄용 쪽 번호(.pf-n-print) · 화면 낭독기 전용 글자와 새 탭 아이콘은 뺌
//   같은 덱 안 링크(목차·장 표지) → 그 슬라이드로 가는 링크, 다른 페이지 링크 → 지금 주소 기준 절대 주소(로컬 미리보기에서 만들면 localhost 주소)
//   라이브러리: PptxGenJS 3.12.0(jsdelivr, 버전 고정) — 단추를 누를 때만 받음
//   글꼴: 화면과 같은 이름(Pretendard·D2Coding·Montserrat·테마 글꼴)을 적습니다. 그 글꼴이 없는 PC에서는 PowerPoint가 다른 글꼴로 보여 주므로
//         줄바꿈이 달라질 수 있습니다(글꼴 설치 또는 PowerPoint [바꾸기 → 글꼴 바꾸기]).
//   점검: await PFPptx.build() → { pptx, report } (내려받지 않음) · PFPptx.report(마지막으로 만든 파일 — 슬라이드별 개체 수·경고·글꼴)
//         헤드리스 Chrome(--virtual-time-budget)은 그림을 받는 동안 가상 시간이 흘러 끝까지 만들지 못하므로 통로를 두지 않음
(() => {
  const LIB = 'https://cdn.jsdelivr.net/npm/pptxgenjs@3.12.0/dist/pptxgen.bundle.js';
  const W = 1280, H = 720, DPI = 96, PT = 0.75;                          // 1280×720px = 13.333×7.5in(LAYOUT_WIDE), 1px = 0.75pt
  const SVGNS = 'http://www.w3.org/2000/svg';
  const IN = (px) => Math.round((px / DPI) * 10000) / 10000;
  const P2 = (px) => Math.round(px * PT * 100) / 100;
  const L = (k, v) => (typeof PFStr === 'object' ? PFStr.t(k, v) : k);
  const num = (v) => parseFloat(v) || 0;
  const SKIP = '.pf-sr-only, .pf-sr, .pf-screen-only, .pf-n-screen, .pf-skiplink, .pf-newtab-icon, [hidden]';
  const VOID = new Set(['img', 'br', 'hr', 'input', 'video', 'audio', 'canvas', 'iframe', 'object', 'embed', 'source', 'track', 'wbr', 'area', 'select', 'textarea']);
  const BLOCKY = /^(block|flex|grid|list-item|table|flow-root|table-row-group|table-header-group|table-footer-group|table-row|table-cell|table-caption)$/;
  const GENERIC = /^(serif|sans-serif|monospace|cursive|fantasy|system-ui|ui-[a-z-]+|-apple-system|blinkmacsystemfont|emoji|math|fangsong|apple color emoji|segoe ui emoji|segoe ui symbol|noto color emoji)$/i;

  // ── 라이브러리(필요할 때 한 번) ──
  let libP = null;
  const loadLib = () => libP || (libP = new Promise((ok, no) => {
    if (window.PptxGenJS) { ok(window.PptxGenJS); return; }
    const s = document.createElement('script');
    s.src = LIB; s.async = true;
    s.onload = () => (window.PptxGenJS ? ok(window.PptxGenJS) : no(new Error('PowerPoint 라이브러리를 읽지 못했습니다')));
    s.onerror = () => { libP = null; s.remove(); no(new Error('PowerPoint 라이브러리를 받지 못했습니다(인터넷 연결을 확인해 주세요)')); };
    document.head.appendChild(s);
  }));

  // ── 색: 어떤 CSS 색 표기든(rgb·color(srgb)·color-mix·oklch…) 캔버스에 칠해 RGBA로 ──
  const cv = document.createElement('canvas'); cv.width = cv.height = 1;
  const g2 = cv.getContext('2d', { willReadFrequently: true });
  const colors = new Map();
  function color(str) {
    if (!str || str === 'transparent' || str === 'none' || str === 'currentcolor') return null;
    if (colors.has(str)) return colors.get(str);
    let v = null;
    g2.fillStyle = '#010203'; g2.fillStyle = str;
    if (g2.fillStyle !== '#010203' || /^#010203$/i.test(str)) {
      g2.clearRect(0, 0, 1, 1); g2.fillRect(0, 0, 1, 1);
      const d = g2.getImageData(0, 0, 1, 1).data;
      if (d[3] > 2) v = { hex: [d[0], d[1], d[2]].map((n) => n.toString(16).padStart(2, '0')).join('').toUpperCase(), a: d[3] / 255 };
    }
    colors.set(str, v);
    return v;
  }
  const FIRST = /(rgba?\([^)]*\)|hsla?\([^)]*\)|color\([^)]*\)|(?:ok)?(?:lab|lch)\([^)]*\)|#[0-9a-f]{3,8}\b)/i;
  const firstColor = (s) => { const m = FIRST.exec(s || ''); return m ? color(m[1]) : null; };
  const TR = (a) => Math.max(0, Math.min(100, Math.round((1 - a) * 100)));
  const clipText = (cs) => /text/.test(`${cs.backgroundClip} ${cs.webkitBackgroundClip || ''}`);

  // 글꼴 이름: CSS 목록에서 첫 실제 글꼴. 일반 이름뿐이면 Windows 기본 글꼴
  function face(ff) {
    const list = String(ff || '').split(',').map((s) => s.trim().replace(/^['"]|['"]$/g, ''));
    const f = list.find((x) => x && !GENERIC.test(x));
    if (!f) return list.some((x) => /mono/i.test(x)) ? 'Consolas' : 'Malgun Gothic';
    return /^Pretendard( Variable| Std| JP)?$/i.test(f) ? 'Pretendard' : f;
  }

  // ── 장면 하나를 재는 동안의 상태 ──
  let S = null;
  const rel = (q) => ({ x: (q.left - S.ox) / S.k, y: (q.top - S.oy) / S.k, w: q.width / S.k, h: q.height / S.k });
  const boxOf = (el) => rel(el.getBoundingClientRect());
  const warn = (msg) => { if (!S.warn.includes(msg)) S.warn.push(msg); };

  // 변형(transform)·zoom 아래의 글자 배율: 가장 가까운 변형 조상마다 한 번 재서(100em 자) 계산값 1px이 장면에서 몇 px인지
  function ratioOf(el) {
    let b = el;
    for (; b && b !== S.scene; b = b.parentElement) {
      const cs = getComputedStyle(b);
      if (cs.transform !== 'none' || (cs.zoom && cs.zoom !== '1' && cs.zoom !== 'normal')) break;
    }
    if (!b || b === S.scene) return 1 / S.k;
    if (S.ratio.has(b)) return S.ratio.get(b);
    let r;
    if (!VOID.has(b.localName) && !(b instanceof SVGElement)) {
      const p = document.createElement('span');
      p.style.cssText = 'position:absolute;left:0;top:0;display:block;width:100em;height:0;margin:0;padding:0;border:0;font-size:16px;visibility:hidden';
      b.appendChild(p);
      r = p.getBoundingClientRect().width / 100 / num(getComputedStyle(p).fontSize);
      p.remove();
    } else r = b.getBoundingClientRect().width / (b.offsetWidth || b.getBoundingClientRect().width || 1);
    r = (r || 1) / S.k;
    S.ratio.set(b, r);
    return r;
  }

  const radiusOf = (cs, r, k) => {
    const v = String(cs.borderTopLeftRadius || '0').split(' ')[0];
    const px = /%$/.test(v) ? (num(v) / 100) * Math.min(r.w, r.h) : num(v) * k;
    return Math.max(0, Math.min(px, Math.min(r.w, r.h) / 2));
  };
  // box-shadow: inset 0 0 0 Npx 색 → 테두리 한 줄(코드 상자 등). 그 밖의 그림자는 옮기지 않음
  function insetRing(bs, k) {
    if (!bs || bs === 'none') return null;
    const m = /^(.*?)\s+0px\s+0px\s+0px\s+([\d.]+)px\s+inset/.exec(bs.split(/,(?![^(]*\))/)[0].trim());
    if (!m) return null;
    const c = color(m[1]);
    return c ? { w: num(m[2]) * k, c, dash: 'solid' } : null;
  }

  // ── 그러데이션 배경 → 그림(SVG foreignObject로 브라우저가 그린 것을 캔버스에) ──
  const loadImg = (src) => new Promise((ok, no) => { const i = new Image(); i.onload = () => ok(i); i.onerror = () => no(new Error('image')); i.src = src; });
  async function rasterBg(el, cs, { jpeg = false } = {}) {
    const w = el.offsetWidth, h = el.offsetHeight;
    if (!w || !h) return null;
    const sc = jpeg ? 1 : Math.min(2, 2000 / Math.max(w, h));
    const css = ['background-color', 'background-image', 'background-size', 'background-position', 'background-repeat', 'background-origin', 'background-clip',
      'border-top-left-radius', 'border-top-right-radius', 'border-bottom-right-radius', 'border-bottom-left-radius']
      .map((p) => `${p}:${cs.getPropertyValue(p)}`).join(';').replace(/url\([^)]*\)/g, 'none');
    const W2 = Math.round(w * sc), H2 = Math.round(h * sc);
    const svg = `<svg xmlns="${SVGNS}" width="${W2}" height="${H2}"><foreignObject x="0" y="0" width="${W2}" height="${H2}"><div xmlns="http://www.w3.org/1999/xhtml" style="${
      `width:${w}px;height:${h}px;transform:scale(${sc});transform-origin:0 0;${css}`.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;')}"></div></foreignObject></svg>`;
    try {
      const img = await loadImg('data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg));
      const c = document.createElement('canvas'); c.width = W2; c.height = H2;
      c.getContext('2d').drawImage(img, 0, 0);
      return jpeg ? c.toDataURL('image/jpeg', 0.92) : c.toDataURL('image/png');
    } catch { return null; }
  }

  // ── 그림 파일 → data URL(PowerPoint가 읽는 PNG·JPEG·GIF는 원본 그대로, 그 밖은 PNG로) ──
  const files = new Map();
  const toDataUrl = (blob) => new Promise((ok, no) => { const fr = new FileReader(); fr.onload = () => ok(fr.result); fr.onerror = () => no(fr.error); fr.readAsDataURL(blob); });
  function sniff(b) {
    if (b[0] === 0x89 && b[1] === 0x50 && b[2] === 0x4E && b[3] === 0x47) return 'image/png';
    if (b[0] === 0xFF && b[1] === 0xD8) return 'image/jpeg';
    if (b[0] === 0x47 && b[1] === 0x49 && b[2] === 0x46) return 'image/gif';
    return '';
  }
  function imgData(src, el) {
    if (files.has(src)) return files.get(src);
    const p = (async () => {
      try {
        const res = await fetch(src);
        if (!res.ok) throw new Error(String(res.status));
        const buf = await res.arrayBuffer();
        const type = sniff(new Uint8Array(buf, 0, Math.min(8, buf.byteLength)));
        if (type) return toDataUrl(new Blob([buf], { type }));
      } catch { /* 아래 캔버스로 */ }
      try {                                                             // WebP·BMP 등 또는 받지 못한 그림: 화면의 그림을 PNG로
        const c = document.createElement('canvas'); c.width = el.naturalWidth; c.height = el.naturalHeight;
        c.getContext('2d').drawImage(el, 0, 0);
        return c.toDataURL('image/png');
      } catch { return null; }
    })();
    files.set(src, p);
    return p;
  }

  // ── 글자 조각(run) 모으기 ──
  function strContent(content) {                                        // CSS content의 "문자열"만(counter()·attr()은 건너뜀)
    if (!content || content === 'none' || content === 'normal') return '';
    const parts = content.match(/"((?:[^"\\]|\\.)*)"/g);
    if (!parts || content.replace(/"((?:[^"\\]|\\.)*)"/g, '').trim()) return '';
    return parts.map((p) => p.slice(1, -1).replace(/\\([0-9a-f]{1,6})\s?/gi, (_, h) => String.fromCodePoint(parseInt(h, 16))).replace(/\\(.)/g, '$1')).join('');
  }
  function runOpts(cs, k, ctx) {
    let c = ctx.grad || null;
    if (!c) {
      const fill = cs.webkitTextFillColor;
      c = fill && fill !== cs.color && color(fill) ? color(fill) : color(cs.color);
    }
    const ls = cs.letterSpacing === 'normal' ? 0 : num(cs.letterSpacing) * k;
    const deco = cs.textDecorationLine || '';
    const o = { fontFace: face(cs.fontFamily), fontSize: Math.max(1, P2(num(cs.fontSize) * k)), bold: num(cs.fontWeight) >= 600, italic: cs.fontStyle === 'italic' };   // sz 1pt 미만은 PowerPoint가 파일을 고치려 듦
    if (c) { o.color = c.hex; const t = TR(c.a * (ctx.op ?? 1)); if (t) o.transparency = t; }
    if (ls) o.charSpacing = P2(ls);
    if (/underline/.test(deco)) o.underline = { style: 'sng' };
    if (/line-through/.test(deco)) o.strike = 'sngStrike';
    if (cs.verticalAlign === 'super') o.superscript = true; else if (cs.verticalAlign === 'sub') o.subscript = true;
    if (ctx.hl) o.highlight = ctx.hl;
    if (ctx.link) { const h = linkOf(ctx.link); if (h) { o.hyperlink = h; if (!o.underline) o.underline = { style: 'none' }; } }
    return o;
  }
  function linkOf(href) {
    if (!href) return null;
    if (href.startsWith('#')) {
      let id = href.slice(1); try { id = decodeURIComponent(id); } catch { /* 그대로 */ }
      id = id.replace(/^s=/, '');
      const n = S.ids.get(id);
      return n ? { slide: n } : null;
    }
    try { const u = new URL(href, location.href); return /^(https?|mailto):$/.test(u.protocol) ? { url: u.href } : null; } catch { return null; }
  }
  const caseOf = (t, tt) => (tt === 'uppercase' ? t.toUpperCase() : tt === 'lowercase' ? t.toLowerCase() : tt === 'capitalize' ? t.replace(/(^|\s)(\S)/g, (m, a, b) => a + b.toUpperCase()) : t);
  function isInline(n, ncs, hasText) {
    if (ncs.position === 'absolute' || ncs.position === 'fixed' || ncs.float !== 'none') return false;
    if (n.localName === 'img' || n.localName === 'svg') return ncs.display === 'inline' && hasText;
    if (ncs.display === 'inline' || ncs.display === 'contents') return ![...n.querySelectorAll('*')].some((d) => BLOCKY.test(getComputedStyle(d).display));
    return hasText && /^inline-(block|flex|grid)$/.test(ncs.display);
  }
  // nodes → runs [{text, o, pre} | {br}]. 글 사이 그림은 media로 따로
  function collect(node, out, ctx, k, media) {
    if (node.nodeType === 3) {
      const pe = node.parentElement, cs = getComputedStyle(pe);
      if (cs.visibility === 'hidden') return;
      const pre = /^(pre|pre-wrap|break-spaces)$/.test(cs.whiteSpace);
      let t = node.data;
      if (cs.whiteSpace === 'pre-line') t = t.replace(/[ \t]+/g, ' ');
      else if (!pre) t = t.replace(/[\t\n\r\f ]+/g, ' ');
      t = caseOf(t, cs.textTransform);
      if (!t) return;
      const o = runOpts(cs, k, ctx);
      if (pre || cs.whiteSpace === 'pre-line') t.split('\n').forEach((x, i) => { if (i) out.push({ br: true }); if (x) out.push({ text: x.replace(/\t/g, '    '), o, pre: true }); });
      else out.push({ text: t, o });
      return;
    }
    if (node.nodeType !== 1 || node.matches(SKIP)) return;
    const el = node, tag = el.localName;
    if (tag === 'br') { out.push({ br: true }); return; }
    if (tag === 'img' || tag === 'svg' || tag === 'video' || tag === 'canvas') { media.push(el); return; }
    const cs = getComputedStyle(el);
    if (cs.display === 'none') return;
    const sub = { ...ctx };
    if (tag === 'a' && el.hasAttribute('href')) sub.link = el.getAttribute('href');
    if (clipText(cs)) sub.grad = firstColor(cs.backgroundImage);
    else { const bg = color(cs.backgroundColor); if (bg && bg.a > 0.05) sub.hl = bg.hex; }
    const before = strContent(getComputedStyle(el, '::before').content);
    if (before && getComputedStyle(el, '::before').position !== 'absolute') out.push({ text: before, o: runOpts(getComputedStyle(el, '::before'), k, sub) });
    for (const c of el.childNodes) collect(c, out, sub, k, media);
    const after = strContent(getComputedStyle(el, '::after').content);
    if (after && getComputedStyle(el, '::after').position !== 'absolute') out.push({ text: after, o: runOpts(getComputedStyle(el, '::after'), k, sub) });
  }
  // 공백 정리(브라우저처럼 줄 앞뒤·겹친 공백 없앰)
  function tidy(runs) {
    let sp = true;
    for (const r of runs) {
      if (r.br) { sp = true; continue; }
      if (r.pre) { sp = false; continue; }
      if (sp) r.text = r.text.replace(/^ +/, '');
      if (r.text) sp = / $/.test(r.text);
    }
    for (let i = runs.length - 1; i >= 0; i--) {
      const r = runs[i];
      if (r.br) { runs.splice(i, 1); continue; }
      if (r.pre) break;
      r.text = r.text.replace(/ +$/, '');
      if (r.text) break;
    }
    for (let i = 0; i < runs.length; i++) {                              // 줄바꿈 앞 공백
      if (runs[i].br && i && !runs[i - 1].br && !runs[i - 1].pre) runs[i - 1].text = runs[i - 1].text.replace(/ +$/, '');
    }
    return runs.filter((r) => r.br || r.text);
  }
  // 글자 사각형들 → 줄 묶음
  function lineBoxes(rects) {
    const rs = rects.filter((q) => q.width > 0.3 && q.height > 0.3).sort((a, b) => a.top - b.top);
    const out = [];
    for (const q of rs) {
      const c = q.top + q.height / 2;
      const ln = out.find((l) => Math.abs(l.c - c) < Math.min(l.h, q.height) * 0.5);
      if (ln) { ln.top = Math.min(ln.top, q.top); ln.bottom = Math.max(ln.bottom, q.bottom); ln.left = Math.min(ln.left, q.left); ln.right = Math.max(ln.right, q.right); ln.h = ln.bottom - ln.top; }
      else out.push({ c, top: q.top, bottom: q.bottom, left: q.left, right: q.right, h: q.height });
    }
    return out.sort((a, b) => a.top - b.top);
  }
  const ALIGN = { left: 'left', start: 'left', right: 'right', end: 'right', center: 'center', justify: 'justify', '-webkit-center': 'center' };

  // 블록 하나의 글 묶음 → 글상자 항목
  function textGroup(el, cs, nodes, ctx) {
    const k = ratioOf(el), media = [];
    let grad = null;
    for (let e = el; e && e !== S.scene.parentElement; e = e.parentElement) { const s = getComputedStyle(e); if (clipText(s)) { grad = firstColor(s.backgroundImage); break; } if (BLOCKY.test(s.display) && e !== el) break; }
    const base = { link: ctx.link, grad, op: ctx.op };
    let runs = [];
    const pb = getComputedStyle(el, '::before');
    if (strContent(pb.content) && pb.position !== 'absolute' && pb.display !== 'none') runs.push({ text: strContent(pb.content), o: runOpts(pb, k, base) });
    nodes.forEach((n) => collect(n, runs, base, k, media));
    const pa = getComputedStyle(el, '::after');
    if (strContent(pa.content) && pa.position !== 'absolute' && pa.display !== 'none') runs.push({ text: strContent(pa.content), o: runOpts(pa, k, base) });
    runs = tidy(runs);
    const item = runs.some((r) => r.text && r.text.trim()) ? (() => {
      const rng = document.createRange();
      rng.setStartBefore(nodes[0]); rng.setEndAfter(nodes[nodes.length - 1]);
      const lines = lineBoxes([...rng.getClientRects()]);
      if (!lines.length) return null;
      const u = { left: Math.min(...lines.map((l) => l.left)), right: Math.max(...lines.map((l) => l.right)), top: lines[0].top, bottom: lines[lines.length - 1].bottom };
      const cr = el.getBoundingClientRect(), kk = k * S.k;
      const cl = cr.left + (num(cs.borderLeftWidth) + num(cs.paddingLeft)) * kk, crr = cr.right - (num(cs.borderRightWidth) + num(cs.paddingRight)) * kk;
      const align = ALIGN[cs.textAlign] || 'left';
      const flexish = /flex|grid/.test(cs.display) && el.children.length > 0;
      let x0, x1;
      if (flexish) { x0 = u.left; x1 = u.right; }
      else { x0 = cl; x1 = crr; if (align === 'left' && u.left - cl > 3 * kk) x0 = u.left; }
      if (x1 - x0 < u.right - u.left) { if (align === 'center') { const m = (x0 + x1) / 2, hw = (u.right - u.left) / 2; x0 = m - hw; x1 = m + hw; } else x1 = x0 + (u.right - u.left); }
      const fs = Math.max(...runs.filter((r) => r.o).map((r) => r.o.fontSize / PT), 1);
      let lh = cs.lineHeight === 'normal' ? fs * 1.2 : num(cs.lineHeight) * k;
      if (lines.length > 1) lh = (lines[lines.length - 1].top - lines[0].top) / S.k / (lines.length - 1) || lh;
      lh = Math.max(lh, fs);
      const half = Math.max(0, (lh - lines[0].h / S.k) / 2);
      // 줄바꿈: 브라우저에서 한 줄이고 상자를 거의 다 채우면(칩·바닥글·꽉 찬 제목) 끔 — 글꼴 폭이 조금만 달라도 두 줄로 꺾이지 않게
      const tight = lines.length === 1 && (u.right - u.left) > 0.9 * (x1 - x0);
      const nowrap = /^(nowrap|pre)$/.test(cs.whiteSpace) || tight;
      const slack = align === 'center' ? 1 : 0;
      return { t: 'text', runs, x: (x0 - S.ox) / S.k - slack, y: (u.top - S.oy) / S.k - half, w: (x1 - x0) / S.k + 1 + 2 * slack,
        h: Math.max(lines.length * lh, (u.bottom - u.top) / S.k + 2 * half), align, lh, wrap: !nowrap, op: ctx.op, lines: lines.length };
    })() : null;
    return { item, media };
  }

  // ── 상자(배경·테두리) ──
  async function paintBox(el, cs, r, ctx) {
    if (r.w < 0.5 || r.h < 0.5) return null;
    const k = ratioOf(el);
    const ct = clipText(cs);
    const bg = ct ? null : color(cs.backgroundColor);
    const bgi = ct ? 'none' : cs.backgroundImage;
    const sides = ['Top', 'Right', 'Bottom', 'Left'].map((s) => {
      const st = cs[`border${s}Style`], w = num(cs[`border${s}Width`]) * k;
      if (!w || st === 'none' || st === 'hidden') return null;
      const c = color(cs[`border${s}Color`]);
      return c ? { w, c, dash: st === 'dashed' ? 'dash' : st === 'dotted' ? 'sysDot' : 'solid' } : null;
    });
    const bimg = cs.borderImageSource && cs.borderImageSource !== 'none' ? firstColor(cs.borderImageSource) : null;
    if (bimg) sides.forEach((s, i) => { if (s) sides[i] = { ...s, c: bimg }; });
    const s0 = sides[0];
    const uniform = s0 && sides.every((s) => s && Math.abs(s.w - s0.w) < 0.01 && s.c.hex === s0.c.hex && s.c.a === s0.c.a) ? s0 : null;
    const line = uniform || (sides.every((s) => !s) ? insetRing(cs.boxShadow, k) : null);
    const rad = radiusOf(cs, r, k);
    let shape = null;
    if (bgi && bgi !== 'none' && /gradient\(/.test(bgi)) {
      const data = await rasterBg(el, cs);
      if (data) S.items.push({ t: 'pic', data, x: r.x, y: r.y, w: r.w, h: r.h, op: ctx.op, name: 'gradient' });
      else { const c = firstColor(bgi) || bg; if (c) S.items.push(shape = { t: 'shape', ...r, rad, fill: c, line, op: ctx.op }); }
      if (line && !shape) S.items.push({ t: 'shape', ...r, rad, fill: null, line, op: ctx.op });
    } else if (bg || line) S.items.push(shape = { t: 'shape', ...r, rad, fill: bg, line, op: ctx.op });
    if (!uniform) {                                                     // 한쪽만 있는 테두리(강조 막대·구분선)
      const [t, rt, b, l] = sides;
      if (t) S.items.push({ t: 'shape', x: r.x, y: r.y, w: r.w, h: t.w, fill: t.c, op: ctx.op });
      if (b) S.items.push({ t: 'shape', x: r.x, y: r.y + r.h - b.w, w: r.w, h: b.w, fill: b.c, op: ctx.op });
      if (l) S.items.push({ t: 'shape', x: r.x, y: r.y, w: l.w, h: r.h, fill: l.c, op: ctx.op });
      if (rt) S.items.push({ t: 'shape', x: r.x + r.w - rt.w, y: r.y, w: rt.w, h: r.h, fill: rt.c, op: ctx.op });
    }
    return shape;
  }
  // 위치를 가진 ::before/::after(✓ 칩 등) → 도형 + 글
  function pseudoAbs(el, cs, r, ctx) {
    if (cs.position === 'static') return;
    const k = ratioOf(el);
    for (const which of ['::before', '::after']) {
      const p = getComputedStyle(el, which);
      if (p.position !== 'absolute' || p.display === 'none' || !p.content || p.content === 'none' || p.content === 'normal') continue;
      const left = num(p.left), top = num(p.top), w = num(p.width), h = num(p.height);
      if (p.left === 'auto' || p.top === 'auto' || !w || !h) continue;
      const x0 = r.x + num(cs.borderLeftWidth) * k, y0 = r.y + num(cs.borderTopWidth) * k;
      const box = { x: x0 + left * k, y: y0 + top * k, w: w * k, h: h * k };
      const text = strContent(p.content);
      const bg = color(p.backgroundColor);
      S.items.push({ t: 'shape', ...box, rad: radiusOf(p, box, k), fill: bg, line: null, op: ctx.op,
        text: text ? { runs: [{ text, o: runOpts(p, k, { op: ctx.op }) }], align: 'center', valign: 'middle', inset: [0, 0, 0, 0], wrap: false } : null });
    }
  }

  // ── 그림 ──
  async function picture(img, cs, r, ctx) {
    const src = img.currentSrc || img.src;
    if (!src || !img.naturalWidth) { warn(`그림을 불러오지 못해 뺐습니다: ${img.getAttribute('src') || ''}`); return; }
    const k = ratioOf(img);
    const b = { x: r.x + (num(cs.borderLeftWidth) + num(cs.paddingLeft)) * k, y: r.y + (num(cs.borderTopWidth) + num(cs.paddingTop)) * k };
    b.w = r.w - (num(cs.borderLeftWidth) + num(cs.paddingLeft) + num(cs.borderRightWidth) + num(cs.paddingRight)) * k;
    b.h = r.h - (num(cs.borderTopWidth) + num(cs.paddingTop) + num(cs.borderBottomWidth) + num(cs.paddingBottom)) * k;
    const nw = img.naturalWidth, nh = img.naturalHeight, fit = cs.objectFit;
    const [px, py] = String(cs.objectPosition || '50% 50%').split(' ').map((v) => (/%$/.test(v) ? num(v) / 100 : 0.5));
    let d = { ...b }, crop = null;
    if (fit === 'contain' || fit === 'scale-down') {
      let s = Math.min(b.w / nw, b.h / nh);
      if (fit === 'scale-down') s = Math.min(s, k);
      d = { w: nw * s, h: nh * s }; d.x = b.x + (b.w - d.w) * px; d.y = b.y + (b.h - d.h) * py;
    } else if (fit === 'cover' || fit === 'none') {
      const s = fit === 'cover' ? Math.max(b.w / nw, b.h / nh) : k;
      const fw = nw * s, fh = nh * s;
      if (fw > b.w + 0.5 || fh > b.h + 0.5) crop = { x: Math.max(0, (fw - b.w) * px), y: Math.max(0, (fh - b.h) * py), w: Math.min(b.w, fw), h: Math.min(b.h, fh), fw, fh };
      else { d = { w: fw, h: fh }; d.x = b.x + (b.w - fw) * px; d.y = b.y + (b.h - fh) * py; }
    }
    const data = await imgData(src, img);
    if (!data) { warn(`그림을 PowerPoint에 넣지 못했습니다: ${img.getAttribute('src') || ''}`); return; }
    S.items.push({ t: 'pic', data, x: crop ? b.x : d.x, y: crop ? b.y : d.y, w: d.w, h: d.h, crop, op: ctx.op, alt: img.alt || '', link: ctx.link });
  }

  // ── SVG(다이어그램·아이콘) → 스타일을 속성으로 굳힌 독립 SVG. foreignObject 글(mermaid 라벨)은 화면 위치 그대로 SVG 글자로 ──
  const SVGP = ['fill', 'fill-opacity', 'fill-rule', 'stroke', 'stroke-width', 'stroke-opacity', 'stroke-dasharray', 'stroke-dashoffset', 'stroke-linecap', 'stroke-linejoin',
    'stroke-miterlimit', 'opacity', 'font-family', 'font-size', 'font-weight', 'font-style', 'text-anchor', 'dominant-baseline', 'alignment-baseline', 'baseline-shift',
    'letter-spacing', 'visibility', 'display', 'marker-start', 'marker-mid', 'marker-end', 'stop-color', 'stop-opacity', 'transform', 'transform-origin'];
  function foPlan(fo) {
    const m = fo.parentNode?.getScreenCTM?.(); if (!m) return null;
    const inv = m.inverse(), P = (x, y) => new DOMPoint(x, y).matrixTransform(inv);
    const out = [];
    fo.querySelectorAll('*').forEach((e) => {
      const s = getComputedStyle(e), c = color(s.backgroundColor);
      if (!c) return;
      const q = e.getBoundingClientRect(); if (q.width < 0.5 || q.height < 0.5) return;
      const a = P(q.left, q.top), b = P(q.right, q.bottom);
      out.push({ rect: [a.x, a.y, b.x - a.x, b.y - a.y], c });
    });
    const tw = document.createTreeWalker(fo, NodeFilter.SHOW_TEXT);
    const rng = document.createRange();
    for (let t = tw.nextNode(); t; t = tw.nextNode()) {
      if (!t.data.trim()) continue;
      const s = getComputedStyle(t.parentElement);
      if (s.display === 'none' || s.visibility === 'hidden') continue;
      let seg = null;
      const flush = () => { if (seg && seg.text.trim()) out.push(seg); seg = null; };
      for (let i = 0; i < t.data.length; i++) {
        rng.setStart(t, i); rng.setEnd(t, i + 1);
        const q = rng.getBoundingClientRect(), ch = t.data[i];
        if (!q.width && !q.height) continue;
        if (seg && Math.abs(q.top - seg.top) > q.height * 0.5) flush();
        if (!seg) { if (/\s/.test(ch)) continue; seg = { text: '', top: q.top, left: q.left, bottom: q.bottom, s }; }
        seg.text += ch; seg.bottom = Math.max(seg.bottom, q.bottom);
      }
      flush();
    }
    return out.map((o) => {
      if (o.rect) return o;
      const p = P(o.left, o.top + (o.bottom - o.top) * 0.79);
      const c = color(o.s.color) || { hex: '333333', a: 1 };
      return { text: o.text.replace(/\s+$/, ''), x: p.x, y: p.y, size: num(o.s.fontSize), weight: o.s.fontWeight, family: o.s.fontFamily, style: o.s.fontStyle, c };
    });
  }
  function svgData(svg, r) {
    const fos = [...svg.querySelectorAll('foreignObject')].map(foPlan);
    const live = [svg, ...svg.querySelectorAll('*')];
    const cl = svg.cloneNode(true);
    const copy = [cl, ...cl.querySelectorAll('*')];
    live.forEach((e, i) => {
      if (e.closest('foreignObject')) return;
      const s = getComputedStyle(e), d = copy[i];
      const st = SVGP.map((p) => { let v = s.getPropertyValue(p); if (!v || v === 'normal' && p !== 'font-weight') return ''; if (p === 'transform' && v === 'none') return ''; if (p === 'transform-origin') return ''; return `${p}:${v}`; }).filter(Boolean).join(';');
      if (st) d.setAttribute('style', st);
    });
    cl.querySelectorAll('style, script').forEach((e) => e.remove());
    [...cl.querySelectorAll('foreignObject')].forEach((fo, j) => {
      const g = document.createElementNS(SVGNS, 'g');
      for (const o of fos[j] || []) {
        if (o.rect) {
          const rc = document.createElementNS(SVGNS, 'rect');
          rc.setAttribute('x', o.rect[0].toFixed(2)); rc.setAttribute('y', o.rect[1].toFixed(2)); rc.setAttribute('width', o.rect[2].toFixed(2)); rc.setAttribute('height', o.rect[3].toFixed(2));
          rc.setAttribute('style', `fill:#${o.c.hex};fill-opacity:${o.c.a.toFixed(3)};stroke:none`);
          g.appendChild(rc);
        } else {
          const tx = document.createElementNS(SVGNS, 'text');
          tx.setAttribute('x', o.x.toFixed(2)); tx.setAttribute('y', o.y.toFixed(2));
          tx.setAttribute('style', `font-family:${o.family};font-size:${o.size}px;font-weight:${o.weight};font-style:${o.style};fill:#${o.c.hex};fill-opacity:${o.c.a.toFixed(3)};stroke:none;white-space:pre`);
          tx.setAttribute('xml:space', 'preserve');
          tx.textContent = o.text;
          g.appendChild(tx);
        }
      }
      fo.replaceWith(g);
    });
    cl.setAttribute('xmlns', SVGNS);
    cl.setAttribute('width', String(Math.round(r.w * 2))); cl.setAttribute('height', String(Math.round(r.h * 2)));
    if (!cl.getAttribute('viewBox')) { const bb = svg.getBoundingClientRect(); cl.setAttribute('viewBox', `0 0 ${bb.width} ${bb.height}`); }
    cl.removeAttribute('class');
    const st = cl.getAttribute('style') || ''; cl.setAttribute('style', st.replace(/(^|;)\s*(max-width|width|height)\s*:[^;]*/g, ''));
    const xml = new XMLSerializer().serializeToString(cl);
    const bytes = new TextEncoder().encode(xml);
    let bin = '';
    for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
    return 'data:image/svg+xml;base64,' + btoa(bin);
  }

  // ── 코드(pre) → 고정폭 글상자, 줄마다 단락 ──
  function codeBox(pre, cs, r, ctx) {
    const code = pre.querySelector('code') || pre;
    const ccs = getComputedStyle(code), k = ratioOf(code);
    const lines = [[]];
    const tw = document.createTreeWalker(code, NodeFilter.SHOW_TEXT);
    const tab = ' '.repeat(Math.max(1, Math.min(8, num(ccs.tabSize) || 4)));
    for (let t = tw.nextNode(); t; t = tw.nextNode()) {
      const o = runOpts(getComputedStyle(t.parentElement), k, { op: ctx.op });
      t.data.replace(/\t/g, tab).split('\n').forEach((x, i) => { if (i) lines.push([]); if (x) lines[lines.length - 1].push({ text: x, o, pre: true }); });
    }
    while (lines.length > 1 && !lines[lines.length - 1].length) lines.pop();
    const base = runOpts(ccs, k, { op: ctx.op });
    const runs = [];
    lines.forEach((ln, i) => { if (i) runs.push({ br: true }); if (ln.length) runs.push(...ln); else runs.push({ text: ' ', o: base, pre: true }); });
    const q = boxOf(code);
    const lh = ccs.lineHeight === 'normal' ? num(ccs.fontSize) * 1.2 * k : num(ccs.lineHeight) * k;
    const pk = ratioOf(pre);
    const inner = r.w - (num(cs.paddingLeft) + num(cs.paddingRight) + num(cs.borderLeftWidth) + num(cs.borderRightWidth)) * pk;
    S.items.push({ t: 'text', runs, x: q.x, y: q.y + (num(ccs.paddingTop) * k), w: Math.max(q.w, inner) + 2, h: Math.max(lines.length * lh, 4), align: 'left', lh,
      wrap: !/^(pre|nowrap)$/.test(ccs.whiteSpace), op: ctx.op, code: true, lines: lines.length });
  }

  // ── 표 → PowerPoint 표(칸 너비·줄 높이·채움·테두리·안쪽 여백 그대로). 칸 안 그림은 표 위에 따로 ──
  function cellRuns(cell, k, ctx, media) {
    const out = [];
    const walkC = (el) => {
      const hasText = [...el.childNodes].some((n) => n.nodeType === 3 && n.data.trim());
      let group = [];
      const flush = () => {
        if (!group.length) return;
        const part = [];
        group.forEach((n) => collect(n, part, ctx, k, media));
        const t = tidy(part);
        if (t.length) { if (out.length) out.push({ br: true }); out.push(...t); }
        group = [];
      };
      for (const n of el.childNodes) {
        if (n.nodeType === 3) { group.push(n); continue; }
        if (n.nodeType !== 1 || n.matches(SKIP)) continue;
        const ncs = getComputedStyle(n);
        if (ncs.display === 'none') continue;
        if (n.localName === 'img' || n.localName === 'svg') { media.push(n); continue; }
        if (isInline(n, ncs, hasText) || ncs.display === 'inline') { group.push(n); continue; }
        flush(); walkC(n);
      }
      flush();
    };
    walkC(cell);
    return out;
  }
  function table(t, ctx) {
    const rows = [...t.rows].filter((r) => getComputedStyle(r).display !== 'none');
    if (!rows.length) return [];
    const media = [];
    const xs = [];
    rows.forEach((r) => [...r.cells].forEach((c) => { const q = boxOf(c); xs.push(q.x, q.x + q.w); }));
    xs.sort((a, b) => a - b);
    const edges = [];
    xs.forEach((x) => { if (!edges.length || x - edges[edges.length - 1] > 1.5) edges.push(x); });
    const colW = edges.slice(1).map((x, i) => x - edges[i]);
    const tops = rows.map((r) => boxOf(r));
    const data = rows.map((r) => [...r.cells].filter((c) => getComputedStyle(c).display !== 'none').map((c) => {
      const cs = getComputedStyle(c), k = ratioOf(c);
      let fill = null;
      for (let e = c; e && e !== t.parentElement; e = e.parentElement) { const x = color(getComputedStyle(e).backgroundColor); if (x) { fill = x; break; } }
      const border = ['Top', 'Right', 'Bottom', 'Left'].map((s) => {
        const st = cs[`border${s}Style`], w = num(cs[`border${s}Width`]) * k, c2 = color(cs[`border${s}Color`]);
        return !w || !c2 || st === 'none' || st === 'hidden' ? { type: 'none' } : { type: st === 'dashed' ? 'dash' : 'solid', pt: P2(w), color: c2.hex };
      });
      const runs = cellRuns(c, k, { link: ctx.link, op: ctx.op }, media);
      const o = { fontFace: face(cs.fontFamily), fontSize: Math.max(1, P2(num(cs.fontSize) * k)), color: (color(cs.color) || { hex: '000000' }).hex, bold: num(cs.fontWeight) >= 600,
        align: ALIGN[cs.textAlign] || 'left', valign: cs.verticalAlign === 'middle' ? 'middle' : cs.verticalAlign === 'bottom' ? 'bottom' : 'top',
        margin: ['Top', 'Right', 'Bottom', 'Left'].map((s) => IN(num(cs[`padding${s}`]) * k)), border };
      if (fill) o.fill = { color: fill.hex, transparency: TR(fill.a * ctx.op) };
      if (c.colSpan > 1) o.colspan = c.colSpan;
      if (c.rowSpan > 1) o.rowspan = c.rowSpan;
      return { runs, o };
    }));
    S.items.push({ t: 'table', data, x: edges[0], y: tops[0].y, w: edges[edges.length - 1] - edges[0], colW, rowH: tops.map((q) => q.h), op: ctx.op });
    return media;
  }

  // ── 장면 걷기(그리는 순서 = DOM 순서) ──
  const outside = (r, c) => r.x > c.x + c.w || r.y > c.y + c.h || r.x + r.w < c.x || r.y + r.h < c.y;
  const meet = (a, b) => { const x = Math.max(a.x, b.x), y = Math.max(a.y, b.y); return { x, y, w: Math.max(0, Math.min(a.x + a.w, b.x + b.w) - x), h: Math.max(0, Math.min(a.y + a.h, b.y + b.h) - y) }; };
  async function walk(el, ctx) {
    const cs = getComputedStyle(el);
    if (cs.display === 'none' || el.matches(SKIP)) return;
    const op = ctx.op * (cs.opacity === '' ? 1 : num(cs.opacity));
    if (op < 0.02) return;
    const tag = el.localName, vis = cs.visibility !== 'hidden' && cs.visibility !== 'collapse';
    const sub = { ...ctx, op };
    if (tag === 'a' && el.hasAttribute('href')) sub.link = el.getAttribute('href');
    const r = boxOf(el);
    if (el !== S.scene && cs.display !== 'contents' && (r.w > 0 || r.h > 0) && outside(r, ctx.clip)) return;
    if (el !== S.scene && /hidden|clip|auto|scroll/.test(cs.overflowX + cs.overflowY)) sub.clip = meet(ctx.clip, r);
    let shape = null;
    if (vis && el !== S.scene && tag !== 'svg') shape = await paintBox(el, cs, r, sub);
    if (tag === 'img') { if (vis) await picture(el, cs, r, sub); return; }
    if (tag === 'svg') { if (vis && r.w > 0.5 && r.h > 0.5) S.items.push({ t: 'pic', data: svgData(el, r), x: r.x, y: r.y, w: r.w, h: r.h, op, name: 'svg' }); return; }
    if (tag === 'video' || tag === 'canvas' || tag === 'iframe') { warn('동영상·캔버스는 옮기지 않았습니다'); return; }
    if (tag === 'table') { const media = vis ? table(el, sub) : []; for (const m of media) await walk(m, sub); return; }
    if (tag === 'pre') { if (vis) codeBox(el, cs, r, sub); return; }
    if (vis) pseudoAbs(el, cs, r, sub);
    const kids = [...el.childNodes];
    const hasText = kids.some((n) => n.nodeType === 3 && n.data.trim());
    const onlyText = shape && !shape.text && kids.every((n) => n.nodeType === 3 || (n.nodeType === 1 && (n.matches(SKIP) || (() => { const s = getComputedStyle(n); return s.display === 'none' || isInline(n, s, true) && n.localName !== 'img' && n.localName !== 'svg'; })())));
    let group = [];
    const flush = async () => {
      if (!group.length) return;
      const { item, media } = textGroup(el, cs, group, sub);
      group = [];
      if (item) {
        if (onlyText && shape && vis) {                                   // 글만 든 상자(칩·배지·한 줄 카드): 도형 안에 글
          shape.text = { runs: item.runs, align: item.align, valign: 'top', lh: item.lh, wrap: item.wrap,
            inset: [Math.max(0, item.x - shape.x), Math.max(0, shape.x + shape.w - item.x - item.w), Math.max(0, shape.y + shape.h - item.y - item.h), Math.max(0, item.y - shape.y)] };
          if (item.w > shape.w) shape.text.wrap = false;
        } else S.items.push(item);                                       // 숨긴 글은 collect가 이미 뺌
      }
      for (const m of media) await walk(m, sub);
    };
    for (const n of kids) {
      if (n.nodeType === 3) { group.push(n); continue; }
      if (n.nodeType !== 1 || n.matches(SKIP)) continue;
      const ncs = getComputedStyle(n);
      if (ncs.display === 'none') continue;
      if (isInline(n, ncs, hasText)) { group.push(n); continue; }
      await flush();
      await walk(n, sub);
    }
    await flush();
  }

  // ── 장면 복제(화면 밖 배율 1, 인쇄와 같은 상태) ──
  function prepClone(scene) {
    const c = scene.cloneNode(true);
    c.classList.remove('pf-cur', 'pf-enter');
    c.style.transform = 'none'; c.style.visibility = 'visible'; c.style.margin = '0'; c.style.boxShadow = 'none';
    c.querySelectorAll('.pf-n-print').forEach((e) => { e.style.display = 'inline'; });
    c.querySelectorAll('[data-pf-reveal]').forEach((e) => { e.style.opacity = '1'; e.style.transform = 'none'; });
    c.querySelectorAll('img').forEach((i) => { i.loading = 'eager'; });
    return c;
  }
  const settle = (imgs) => Promise.all(imgs.map((i) => (i.complete ? null : new Promise((r) => { i.addEventListener('load', r, { once: true }); i.addEventListener('error', r, { once: true }); setTimeout(r, 15000); }))));

  async function sceneItems(scene, host, ids) {
    const c = prepClone(scene);
    host.replaceChildren(c);
    await settle([...c.querySelectorAll('img')]);
    const sr = c.getBoundingClientRect();
    S = { scene: c, ox: sr.left, oy: sr.top, k: sr.width / W || 1, items: [], ratio: new Map(), warn: [], ids };
    const cs = getComputedStyle(c);
    let bg = { color: (color(cs.backgroundColor) || { hex: 'FFFFFF' }).hex };
    if (cs.backgroundImage && /gradient\(/.test(cs.backgroundImage)) { const data = await rasterBg(c, cs, { jpeg: true }); if (data) bg = { data }; }
    await walk(c, { op: 1, link: null, clip: { x: 0, y: 0, w: W, h: H } });
    return { bg, items: S.items, warn: S.warn };
  }

  // ── 항목 → PowerPoint ──
  function pRuns(runs, fallback) {
    const out = [];
    for (const r of runs) {
      if (r.br) {
        const last = out[out.length - 1];
        if (!last || last.options.breakLine) out.push({ text: ' ', options: { ...(last ? last.options : fallback), hyperlink: undefined, breakLine: true } });
        else last.options = { ...last.options, breakLine: true };
        continue;
      }
      const text = r.text.replace(/\r/g, '').replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F￾￿]/g, '');   // XML에 못 쓰는 제어 문자
      if (text) out.push({ text, options: { ...r.o } });
    }
    if (!out.length) out.push({ text: ' ', options: { ...fallback } });
    out.forEach((x) => { Object.keys(x.options).forEach((k) => x.options[k] === undefined && delete x.options[k]); });
    return out;
  }
  const lineOpt = (l, op) => (l ? { color: l.c.hex, width: Math.max(0.25, P2(l.w)), transparency: TR(l.c.a * op), dashType: l.dash || 'solid' } : { type: 'none' });
  function emit(pptx, slide, it, stat) {
    const pos = { x: IN(it.x), y: IN(it.y), w: IN(Math.max(it.w, 0.5)), h: IN(Math.max(it.h, 0.5)) };
    if (it.t === 'shape') {
      const o = { ...pos, line: lineOpt(it.line, it.op) };
      if (it.line) { const hw = it.line.w / 2; Object.assign(o, { x: IN(it.x + hw), y: IN(it.y + hw), w: IN(Math.max(it.w - it.line.w, 0.5)), h: IN(Math.max(it.h - it.line.w, 0.5)) }); }
      if (it.fill) o.fill = { color: it.fill.hex, transparency: TR(it.fill.a * it.op) };
      const round = it.rad > 0.5;
      if (round) o.rectRadius = IN(Math.min(it.rad, Math.min(it.w, it.h) / 2));
      const shapeType = round ? pptx.ShapeType.roundRect : pptx.ShapeType.rect;
      if (it.text) {
        // margin 순서(PptxGenJS 3.12): [왼, 오른, 아래, 위] pt — inset도 같은 순서로 모아 둠
        Object.assign(o, { shape: shapeType, align: it.text.align, valign: it.text.valign, wrap: it.text.wrap !== false, margin: it.text.inset.map(P2),
          fit: 'none', paraSpaceBefore: 0, paraSpaceAfter: 0 });
        if (it.text.lh) o.lineSpacing = P2(it.text.lh);
        slide.addText(pRuns(it.text.runs, {}), o); stat.text++; stat.shapes++;
      } else { slide.addShape(shapeType, o); stat.shapes++; }
    } else if (it.t === 'text') {
      slide.addText(pRuns(it.runs, {}), { ...pos, margin: 0, valign: 'top', align: it.align, wrap: it.wrap, fit: 'none', lineSpacing: P2(it.lh), paraSpaceBefore: 0, paraSpaceAfter: 0,
        objectName: it.code ? `Code ${stat.code + 1}` : undefined });
      if (it.code) stat.code++; else stat.text++;
    } else if (it.t === 'pic') {
      const o = { data: it.data, ...pos };
      if (it.crop) Object.assign(o, { w: IN(it.crop.fw), h: IN(it.crop.fh), sizing: { type: 'crop', x: IN(it.crop.x), y: IN(it.crop.y), w: IN(it.crop.w), h: IN(it.crop.h) } });
      if (it.op < 0.99) o.transparency = TR(it.op);
      if (it.alt) o.altText = it.alt;
      const h = it.link && linkOf(it.link); if (h) o.hyperlink = h;
      slide.addImage(o);
      if (it.name === 'svg') stat.svg++; else if (it.name === 'gradient') stat.gradient++; else stat.images++;
    } else if (it.t === 'table') {
      const rows = it.data.map((row) => row.map((c) => ({ text: pRuns(c.runs, { fontFace: c.o.fontFace, fontSize: c.o.fontSize, color: c.o.color }), options: c.o })));
      slide.addTable(rows, { x: IN(it.x), y: IN(it.y), w: IN(it.w), colW: it.colW.map(IN), rowH: it.rowH.map(IN), autoPage: false });
      stat.tables++;
    }
  }

  // ── 만들기 ──
  const fileSafe = (s) => String(s || '').replace(/[\\/:*?"<>|\u0000-\u001f]+/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 120);
  async function build({ onProgress } = {}) {
    const PptxGenJS = await loadLib();
    if (PFScene.whenReady) await PFScene.whenReady();
    const deck = document.getElementById('deck');
    const scenes = [...deck.querySelectorAll(':scope > .pf-scene')].filter((s) => !s.classList.contains('pf-skip'));
    if (!scenes.length) throw new Error('내보낼 슬라이드가 없습니다');
    const ids = new Map(scenes.map((s, i) => [s.id, i + 1]));
    const meta = PFScene.info?.meta || {};
    const pptx = new PptxGenJS();
    pptx.layout = 'LAYOUT_WIDE';
    pptx.title = meta.title || document.title;
    if (meta.name) pptx.author = meta.name;
    pptx.subject = L('name.deck');
    const baseFace = face(getComputedStyle(scenes[0]).fontFamily);
    pptx.theme = { headFontFace: baseFace, bodyFontFace: baseFace };
    const host = document.createElement('div');
    host.dataset.pfUi = 'pptx'; host.setAttribute('aria-hidden', 'true');
    Object.assign(host.style, { position: 'fixed', left: '0', top: '0', width: `${W}px`, height: `${H}px`, overflow: 'hidden', opacity: '0', pointerEvents: 'none', zIndex: '-1', contain: 'layout' });
    document.body.appendChild(host);
    const report = { slides: [], warnings: [], fonts: [], file: '', ms: 0 };
    const t0 = performance.now();
    const fonts = new Set();
    try {
      for (let i = 0; i < scenes.length; i++) {
        onProgress?.(i + 1, scenes.length);
        const { bg, items, warn: w } = await sceneItems(scenes[i], host, ids);
        const slide = pptx.addSlide();
        slide.background = bg;
        const stat = { id: scenes[i].id, text: 0, code: 0, shapes: 0, images: 0, svg: 0, gradient: 0, tables: 0, warnings: w };
        for (const it of items) {
          (it.runs || it.text?.runs || []).forEach((r) => r.o?.fontFace && fonts.add(r.o.fontFace));
          if (it.t === 'table') it.data.forEach((row) => row.forEach((c) => fonts.add(c.o.fontFace)));
          try { emit(pptx, slide, it, stat); } catch (e) { w.push(`개체 하나를 넣지 못했습니다(${it.t}): ${e.message}`); }
        }
        report.slides.push(stat);
        w.forEach((x) => report.warnings.push(`${i + 1}장(${scenes[i].id}): ${x}`));
      }
    } finally { host.remove(); }
    report.fonts = [...fonts];
    report.file = `${fileSafe(meta.title || document.title.split(' · ')[0] || 'deck')} - ${fileSafe(L('name.deck'))}.pptx`;
    report.ms = Math.round(performance.now() - t0);
    PFPptx.report = report;
    return { pptx, report };
  }

  let busy = false;
  async function save(btn) {
    if (busy) return;
    busy = true;
    const say = (t) => PFScene.hudMsg?.(t);
    btn?.setAttribute('aria-disabled', 'true');
    try {
      say(L('ui.pptx-busy', { n: 0, total: document.querySelectorAll('#deck > .pf-scene:not(.pf-skip)').length }));   // 처음 한 번은 라이브러리를 받는 동안
      const { pptx, report } = await build({ onProgress: (n, total) => say(L('ui.pptx-busy', { n, total })) });
      say(L('ui.pptx-pack'));                                            // 그림이 많으면 몇 초~수십 초(움직이는 GIF는 원본 그대로 들어감)
      await pptx.writeFile({ fileName: report.file, compression: true });
      say(L('ui.pptx-done'));
      setTimeout(() => say(''), 4000);
      if (report.warnings.length) console.warn('[pf-pptx]', report.warnings);
    } catch (e) {
      console.error('[pf-pptx]', e);
      say(L('ui.pptx-fail', { msg: e?.message || String(e) }));
      setTimeout(() => say(''), 8000);
    } finally { busy = false; btn?.removeAttribute('aria-disabled'); }
  }

  window.PFPptx = { build, save, report: null, lib: LIB };
})();
