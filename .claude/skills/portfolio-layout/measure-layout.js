// /portfolio-layout — 레퍼런스 웹페이지의 배치 골격 재기
//   브라우저 도구로 레퍼런스 페이지를 열고(화면 1440×900), 이 파일 내용을 통째로 JavaScript로 실행합니다. 마지막 식의 값(JSON)이 결과입니다.
//   사이트에 의존하지 않습니다: 클래스 이름·글·그림 내용·주소는 읽지도 결과에 넣지도 않고, 상자의 위치·크기·배경 유무·글자 크기 같은 숫자만 잽니다.
//   결과: { page, bands[], wireframe, notes[] }
//     bands[i] = 위에서부터의 띠(절) 하나 — role(짐작) · layout(stack 한 단 · split 2단 · columns 3단 이상 · grid 카드 격자 · rows 줄마다 같은 비율로 나뉜 목록)
//                · cols·rows·ratio(%)·gap(px) · media(그림 위치)
//                · height(화면 높이 배수) · contentWidth(px) · centered · align · background(same·tinted·image) · titleScale(본문 글자 대비 가장 큰 글자)
//     wireframe = 사람이 확인할 한 줄씩의 그림(대화에 그대로 붙여 보여 줌)
await (async () => {
  const sleep = (ms) => new Promise((ok) => setTimeout(ok, ms));
  const jump = (y) => scrollTo({ top: y, left: 0, behavior: 'instant' });   // scroll-behavior: smooth인 페이지도 바로 이동
  // 늦게 나타나는 요소(지연 로딩·스크롤 등장 효과)를 깨우려고 한 번 끝까지 내려갔다가 돌아옴
  const H0 = document.documentElement.scrollHeight;
  for (let y = 0; y < H0; y += Math.round(innerHeight * 0.7)) { jump(y); await sleep(120); }
  jump(0);
  for (let t = 0; t < 20 && scrollY > 0; t++) { await sleep(50); jump(0); }
  await sleep(300);
  if (document.fonts && document.fonts.ready) { try { await document.fonts.ready; } catch { /* 그대로 */ } }

  const VW = document.documentElement.clientWidth, VH = innerHeight;
  const SKIP = /^(script|style|link|meta|noscript|template|br|head|title|source|track)$/i;
  const MEDIA = /^(img|video|canvas|picture|iframe|object|embed)$/i;
  const st = (el) => getComputedStyle(el);
  const R = (el) => { const r = el.getBoundingClientRect(); return { x: r.left, y: r.top + scrollY, w: r.width, h: r.height }; };
  const shown = (el) => {
    const s = st(el);
    if (s.display === 'none' || s.visibility === 'hidden' || +s.opacity < 0.05) return false;
    const r = el.getBoundingClientRect();
    return r.width > 4 && r.height > 4;
  };
  const kids = (el) => [...el.children].filter((c) => !SKIP.test(c.localName) && shown(c));
  const flowKids = (el) => kids(el).filter((c) => !/absolute|fixed/.test(st(c).position));
  const overlapY = (a, b) => Math.max(0, Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y)) / Math.max(1, Math.min(a.h, b.h));
  // 위아래로 쌓였나: 화면에 붙는 막대(sticky)는 스크롤 위치에 따라 겹쳐 보이므로 판정에서 뺌
  const stacked = (ks) => {
    const flow = ks.filter((k) => st(k).position !== 'sticky');
    return ks.length > 1 && flow.every((k, i) => { if (!i) return true; const a = R(flow[i - 1]), b = R(k); return b.y >= a.y + a.h - 6; });
  };
  const pct = (v) => Math.round(v * 100);
  const median = (xs) => { const s = [...xs].sort((a, b) => a - b); return s.length ? s[Math.floor(s.length / 2)] : 0; };
  const baseFont = parseFloat(st(document.body).fontSize) || 16;
  const bgOf = (el) => { const s = st(el); if (s.backgroundImage && s.backgroundImage !== 'none') return 'image'; const c = s.backgroundColor; return c && c !== 'transparent' && !/rgba\([^)]*,\s*0\)$/.test(c) ? c : null; };
  const pageBg = bgOf(document.body) || bgOf(document.documentElement) || 'rgb(255, 255, 255)';

  // ── 띠 나누기: 겉 상자 한 겹씩 벗기며, 위아래로 쌓인 자식들이 보이면 그것이 띠 ──
  function bandsOf(el, depth) {
    const ks = flowKids(el);
    if (!ks.length || depth > 14) return [el];
    if (ks.length === 1) return bandsOf(ks[0], depth + 1);
    if (stacked(ks)) {
      return ks.flatMap((k) => {
        const kk = flowKids(k);
        return R(k).h > VH * 1.3 && kk.length > 1 && stacked(kk) ? bandsOf(k, depth + 1) : [k];
      });
    }
    return [el];
  }
  let bands = bandsOf(document.body, 0).filter((b) => { const r = R(b); return r.y + r.h > 0 && r.w >= VW * 0.5 && r.h >= 8; });

  // 화면 위에 붙어 있는 머리 막대(position: fixed·sticky)는 흐름 밖이라 따로 찾음
  const fixedNav = [...document.body.querySelectorAll('*')].find((e) => {
    if (SKIP.test(e.localName) || !/fixed|sticky/.test(st(e).position) || !shown(e)) return false;
    const r = e.getBoundingClientRect();
    return r.top <= 10 && r.width >= VW * 0.6 && r.height < 160;
  });
  if (fixedNav && !bands.some((b) => b === fixedNav || b.contains(fixedNav) || fixedNav.contains(b))) bands = [fixedNav, ...bands];

  // ── 띠 하나 재기 ──
  const isMediaEl = (e) => MEDIA.test(e.localName) || (e.localName === 'svg' && e.getBoundingClientRect().width > 48) || (st(e).backgroundImage.includes('url(') && e.getBoundingClientRect().width > 120);
  const hasOwnText = (e) => [...e.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim().length > 0);
  function leafs(band) {
    const out = [];
    for (const e of band.querySelectorAll('*')) {
      if (SKIP.test(e.localName) || e.closest('svg') && e.localName !== 'svg') continue;
      const media = isMediaEl(e), text = hasOwnText(e);
      if ((media || text) && shown(e)) out.push({ e, media, text, r: R(e) });
    }
    return out;
  }
  const areaIn = (r, box) => Math.max(0, Math.min(r.x + r.w, box.x + box.w) - Math.max(r.x, box.x)) * Math.max(0, Math.min(r.y + r.h, box.y + box.h) - Math.max(r.y, box.y));
  function rowsOf(els) {
    const rows = [];
    els.map((e) => ({ e, r: R(e) })).sort((a, b) => a.r.y - b.r.y || a.r.x - b.r.x).forEach((o) => {
      const row = rows.find((rw) => overlapY(rw[0].r, o.r) > 0.5);
      if (row) row.push(o); else rows.push([o]);
    });
    return rows.map((rw) => rw.sort((a, b) => a.r.x - b.r.x));
  }
  // 위아래로 쌓인 줄마다 같은 비율로 옆으로 나뉜 배열(이름 | 설명 목록, 이력 표 같은 것)
  function rowsLike(el, cw) {
    const ks = flowKids(el);
    if (ks.length < 2 || !stacked(ks)) return null;
    const sig = ks.map((k) => {
      let n = k, fk = flowKids(n);
      for (let d = 0; fk.length === 1 && d < 3; d++) { n = fk[0]; fk = flowKids(n); }
      const rw = rowsOf(fk).find((x) => x.length > 1);
      if (!rw) return null;
      const span = rw[rw.length - 1].r.x + rw[rw.length - 1].r.w - rw[0].r.x;
      if (span < cw * 0.5) return null;
      const tot = rw.reduce((t, o) => t + o.r.w, 0);
      return { cols: rw.length, ratio: rw.map((o) => o.r.w / tot), row: rw };
    });
    const ok = sig.filter(Boolean);
    if (ok.length < 2 || ok.length < ks.length * 0.6) return null;
    const ref = ok[0];
    const same = ok.filter((s) => s.cols === ref.cols && s.ratio.every((v, j) => Math.abs(v - ref.ratio[j]) < 0.08));
    if (same.length < 2 || same.length < ok.length * 0.7) return null;
    return { kind: 'rows', first: ref.row, count: same.length, area: ks.reduce((t, k) => t + R(k).w * R(k).h, 0) };
  }
  // 띠 안에서 가장 넓은 면적을 차지하는 "옆으로 나란한" 배열(단 나눔·카드 격자·줄마다 나뉜 목록)
  function dominant(band, cw) {
    let best = null;
    const walk = (el, d) => {
      if (d > 8) return;
      const ks = flowKids(el);
      if (ks.length >= 2) {
        const rows = rowsOf(ks), wide = rows.filter((rw) => rw.length > 1);
        if (wide.length) {
          const span = Math.max(...wide.map((rw) => rw[rw.length - 1].r.x + rw[rw.length - 1].r.w - rw[0].r.x));
          const area = rows.flat().reduce((s, o) => s + o.r.w * o.r.h, 0);
          if (span >= cw * 0.5 && (!best || area > best.area)) best = { kind: 'row', rows, area };
        }
        const rl = rowsLike(el, cw);
        if (rl && (!best || rl.area > best.area)) best = rl;
      }
      ks.forEach((k) => walk(k, d + 1));
    };
    walk(band, 0);
    return best;
  }

  const measured = bands.map((b, i) => {
    const r = R(b), s = st(b);
    const L = leafs(b);
    const box = L.length ? (() => { const x0 = Math.min(...L.map((l) => l.r.x)), x1 = Math.max(...L.map((l) => l.r.x + l.r.w)); return { x: x0, w: x1 - x0 }; })() : { x: r.x, w: r.w };
    const cw = Math.max(1, Math.min(box.w, VW));
    const bandBox = { x: 0, y: r.y, w: VW, h: r.h };
    const mediaArea = L.filter((l) => l.media).reduce((t, l) => t + areaIn(l.r, bandBox), 0);
    const texts = L.filter((l) => l.text);
    const maxFont = texts.length ? Math.max(...texts.map((l) => parseFloat(st(l.e).fontSize) || 0)) : 0;
    const centerTexts = texts.filter((l) => st(l.e).textAlign === 'center' || Math.abs(l.r.x + l.r.w / 2 - (box.x + box.w / 2)) < cw * 0.04 && l.r.w < cw * 0.8);
    let bg = null;
    for (let e = b, n = 0; e && n < 4; n++) { bg = bgOf(e); if (bg) break; const k = flowKids(e); e = k.length === 1 ? k[0] : null; }
    if (!bg) { const deco = kids(b).find((c) => /absolute/.test(st(c).position) && R(c).w >= r.w * 0.9 && R(c).h >= r.h * 0.9); if (deco) bg = bgOf(deco) || (deco.querySelector('img,video,canvas') ? 'image' : null); }
    const background = !bg || bg === pageBg ? 'same' : bg === 'image' ? 'image' : 'tinted';

    const dom = dominant(b, cw);
    const domArea = dom ? dom.area : 0;
    const contentArea = Math.max(1, cw * r.h);
    let layout = 'stack', info = {};
    const mediaOf = (o) => { const a = Math.max(1, o.r.w * o.r.h); return [o.e, ...o.e.querySelectorAll('*')].filter((e) => isMediaEl(e) && shown(e)).reduce((t, e) => t + areaIn(R(e), o.r), 0) / a; };
    if (dom && dom.kind === 'rows' && domArea >= contentArea * 0.25) {
      const first = dom.first, tot = first.reduce((t, o) => t + o.r.w, 0), ms = first.map(mediaOf);
      layout = 'rows';
      info = { cols: first.length, rows: dom.count, ratio: first.map((o) => pct(o.r.w / tot)), gap: Math.round(first[1].r.x - (first[0].r.x + first[0].r.w)),
        media: ms[0] > 0.4 && ms[ms.length - 1] < 0.25 ? 'left' : ms[ms.length - 1] > 0.4 && ms[0] < 0.25 ? 'right' : 'none' };
    } else if (dom && domArea >= contentArea * 0.25) {
      const items = dom.rows.flat(), first = dom.rows.find((rw) => rw.length > 1);
      const ws = items.map((o) => o.r.w), cols = Math.max(...dom.rows.map((rw) => rw.length));
      const tot = first.reduce((t, o) => t + o.r.w, 0);
      const ratio = first.map((o) => pct(o.r.w / tot));
      const gap = Math.round(first.length > 1 ? first[1].r.x - (first[0].r.x + first[0].r.w) : 0);
      const ms = first.map(mediaOf);
      if (items.length >= 3 && Math.max(...ws) / Math.max(1, Math.min(...ws)) < 1.25) {
        layout = 'grid';
        info = { cols, rows: dom.rows.length, items: items.length, gap, itemMedia: pct(median(items.map(mediaOf))) };
      } else if (first.length === 2) {
        layout = 'split';
        info = { cols: 2, ratio, gap, media: ms[0] > 0.4 && ms[1] < 0.25 ? 'left' : ms[1] > 0.4 && ms[0] < 0.25 ? 'right' : 'none' };
      } else {
        layout = 'columns';
        info = { cols: first.length, ratio, gap };
      }
    }
    const fixed = /fixed|sticky/.test(s.position) || b === fixedNav;
    const links = [...b.querySelectorAll('a')].filter(shown).length;
    return {
      i: i + 1, y: Math.round(r.y), h: Math.round(r.h), height: Math.round((r.h / VH) * 100) / 100,
      layout, ...info,
      contentWidth: Math.round(cw), centered: Math.abs(box.x + box.w / 2 - VW / 2) < VW * 0.03,
      align: texts.length && centerTexts.length / texts.length > 0.6 ? 'center' : 'left',
      background, mediaShare: pct(Math.min(1, mediaArea / Math.max(1, VW * r.h))),
      titleScale: Math.round((maxFont / baseFont) * 10) / 10, textBlocks: texts.length, links, fixed,
    };
  });

  // ── 역할 짐작(이름만 짐작 — 확인은 사람이) ──
  const firstContent = measured.findIndex((m) => !(m.fixed || (m.i === 1 && m.h < 140 && m.links >= 3)));
  measured.forEach((m, k) => {
    if (m.fixed || (k === 0 && m.h < 140 && m.links >= 3)) m.role = 'nav';
    else if (k === firstContent && m.y < VH * 0.8 && (m.height >= 0.55 || m.titleScale >= 2.5)) m.role = 'hero';
    else if (k === measured.length - 1 && m.height < 0.7 && (m.links >= 4 || m.titleScale <= 1.3)) m.role = 'footer';
    else m.role = m.layout === 'grid' ? 'cards' : m.layout === 'split' ? 'split' : m.layout === 'columns' ? 'columns' : m.layout === 'rows' ? 'list' : m.mediaShare > 50 ? 'media' : 'text';
  });

  // ── 사람이 확인할 한 줄 그림 ──
  const bar = (m) => {
    const W = 30;
    if (m.layout === 'grid') return `[${'▢'.repeat(Math.min(m.cols, 6)).split('').join(' ')}] ×${m.rows}줄`;
    if (m.layout === 'rows') return '[' + m.ratio.map((p) => '─'.repeat(Math.max(3, Math.round((p / 100) * 26)))).join('┼') + `] ×${m.rows}줄`;
    if (m.layout === 'split' || m.layout === 'columns') {
      return '[' + m.ratio.map((p, j) => { const n = Math.max(3, Math.round((p / 100) * W)); const fill = m.media === 'left' && j === 0 || m.media === 'right' && j === m.ratio.length - 1 ? '■' : '·'; return fill.repeat(n); }).join('|') + ']';
    }
    return m.align === 'center' ? `[${' '.repeat(8)}≡≡≡≡≡≡≡≡≡≡≡≡≡≡${' '.repeat(8)}]` : `[≡≡≡≡≡≡≡≡≡≡≡≡≡≡≡≡${' '.repeat(14)}]`;
  };
  const say = (m) => {
    const bits = [];
    if (m.layout === 'grid') bits.push(`카드 ${m.cols}열 × ${m.rows}줄(${m.items}개)` + (m.itemMedia > 40 ? ' · 카드마다 그림' : ''));
    else if (m.layout === 'split') bits.push(`2단 ${m.ratio.join(':')}` + (m.media !== 'none' ? ` · 그림 ${m.media === 'left' ? '왼쪽' : '오른쪽'}` : ''));
    else if (m.layout === 'columns') bits.push(`${m.cols}단 ${m.ratio.join(':')}`);
    else if (m.layout === 'rows') bits.push(`줄마다 ${m.cols}단 ${m.ratio.join(':')} × ${m.rows}줄` + (m.media !== 'none' ? ` · 그림 ${m.media === 'left' ? '왼쪽' : '오른쪽'}` : ''));
    else bits.push(m.align === 'center' ? '한 단 · 가운데 정렬' : '한 단');
    if (m.titleScale >= 1.8) bits.push(`큰 글자 ${m.titleScale}배`);
    if (m.mediaShare >= 30 && m.layout !== 'grid') bits.push(`그림 ${m.mediaShare}%`);
    if (m.background !== 'same') bits.push(m.background === 'image' ? '배경 그림·그러데이션' : '배경색 다름');
    if (m.fixed) bits.push('화면 위 고정');
    return bits.join(' · ');
  };
  const wireframe = measured.map((m) => `${String(m.i).padStart(2)} ${m.role.padEnd(7)} ${bar(m).padEnd(34)} ${say(m)} · 높이 ${m.height}화면`).join('\n');

  const notes = [];
  if (measured.filter((m) => m.role !== 'nav').length < 2) notes.push('띠를 2개 이상 찾지 못했습니다 — 한 화면짜리 앱·캔버스 페이지이거나 스크롤 상자 안에 그려진 페이지일 수 있습니다. 스크린샷 방식으로 하세요');
  if (document.documentElement.scrollWidth > VW + 4) notes.push('가로로 넘치는 페이지입니다');
  if (VW < 1200) notes.push(`화면 폭이 ${VW}px입니다 — 1440px 안팎에서 재야 넓은 화면 배치가 나옵니다`);
  const widths = measured.filter((m) => m.role !== 'nav').map((m) => m.contentWidth);
  return {
    tool: 'portfolio-layout/measure', v: 1, viewport: `${VW}×${VH}`,
    page: { height: Math.round(document.documentElement.scrollHeight), screens: Math.round((document.documentElement.scrollHeight / VH) * 10) / 10, baseFont,
      contentWidth: median(widths), centered: measured.filter((m) => m.centered).length >= measured.length / 2, stickyNav: measured.some((m) => m.role === 'nav' && m.fixed), bands: measured.length },
    bands: measured, wireframe, notes,
  };
})();
