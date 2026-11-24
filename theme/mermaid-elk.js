/* 다이어그램 ELK 배치 (theme/mermaid-elk.js) — 상세 문서(doc.html)·슬라이드(project.html)가 mermaid 다음 줄에 싣습니다. (theme/RENDERER-CHANGES.md 6)
   Mermaid 기본 배치(dagre) 대신 ELK로 노드 위치와 선을 정리합니다. 갈래·되돌아가는 선이 많은 그림에서 선이 덜 겹칩니다.
   - 적용되는 그림: flowchart · stateDiagram(Mermaid가 ELK를 지원하는 종류). sequenceDiagram·erDiagram 등은 원래 배치 그대로입니다.
   - 되돌아가는 선(순환)을 끊는 기준은 GREEDY_MODEL_ORDER입니다(순환을 끊을 때만 문서에 적은 순서를 참고).
     순환이 많아 그림이 엉뚱한 상태부터 시작하면, 그 mermaid 블록 안에 주석 한 줄을 둡니다 → 적은 순서대로 왼쪽(위)부터 놓습니다(MODEL_ORDER).
       %% pf-elk: model-order
     (Mermaid 주석이라 GitHub 등 다른 곳에서는 무시됩니다. MODEL_ORDER는 들어오는 선이 둘인 노드를 뒤집기도 하니 필요한 그림에만)
   - 한 그림만 예전 배치로 두려면 같은 방식으로 `%% pf-elk: off`.
   - ELK를 받지 못하면(네트워크 등) 경고만 남기고 기본 배치로 그립니다. 확인: window.PF_ELK ('ok' | 'fail')
   - 버전: mermaid 11.4.1과 같은 시기의 @mermaid-js/layout-elk 0.1.7(peer mermaid ^11.0.0) */
(() => {
  if (!window.mermaid) return;
  const URL = 'https://cdn.jsdelivr.net/npm/@mermaid-js/layout-elk@0.1.7/dist/mermaid-layout-elk.esm.min.mjs';
  const ready = import(URL)
    .then((m) => { mermaid.registerLayoutLoaders(m.default); window.PF_ELK = 'ok'; })
    .catch((e) => { window.PF_ELK = 'fail'; console.warn('[mermaid-elk] ELK를 불러오지 못해 기본 배치로 그립니다', e); });
  const origInit = mermaid.initialize.bind(mermaid);
  const origRender = mermaid.render.bind(mermaid);
  const origRun = mermaid.run.bind(mermaid);
  let lastCfg = {}, mode = '';
  // mode: '' = 기본(GREEDY_MODEL_ORDER) · 'model-order' · 'off'(dagre)
  const apply = (m) => {
    mode = m;
    const elk = window.PF_ELK !== 'fail' && m !== 'off';
    origInit({ ...lastCfg, ...(elk
      ? { layout: 'elk', elk: { mergeEdges: false, nodePlacementStrategy: 'BRANDES_KOEPF', cycleBreakingStrategy: m === 'model-order' ? 'MODEL_ORDER' : 'GREEDY_MODEL_ORDER', ...(lastCfg.elk || {}) } }
      : { layout: 'dagre' }) });
  };
  const modeOf = (text) => (/^\s*%%\s*pf-elk:\s*(model-order|off)\s*$/m.exec(String(text || '')) || [])[1] || '';
  mermaid.initialize = (cfg = {}) => { lastCfg = cfg; apply(''); };
  // 그리기 직전에 ELK 준비를 기다리고, 그림마다 주석 지정에 맞춰 설정을 바꿈
  mermaid.render = async (id, text, ...rest) => {
    await ready;
    const m = modeOf(text);
    if (m !== mode) apply(m);
    try { return await origRender(id, text, ...rest); } finally { if (mode) apply(''); }
  };
  mermaid.run = async (opts = {}) => {
    await ready;
    const nodes = opts.nodes ? [...opts.nodes] : [...document.querySelectorAll(opts.querySelector || '.mermaid')];
    const groups = new Map();
    nodes.forEach((n) => { const m = modeOf(n.textContent); if (!groups.has(m)) groups.set(m, []); groups.get(m).push(n); });
    try {
      for (const [m, list] of groups) { if (m !== mode) apply(m); await origRun({ ...opts, nodes: list }); }
    } finally { if (mode) apply(''); }
  };
})();
