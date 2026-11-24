# 렌더러를 직접 고친 곳

`/portfolio-update`는 렌더러(`project.html`·`assets/`)를 템플릿 원본으로 되돌립니다.
업데이트를 받은 뒤 아래 두 곳을 다시 넣으면 슬라이드 다듬기가 그대로 돌아옵니다.
(`theme/deck-polish.css` 자체는 업데이트·테마 명령이 건드리지 않습니다.)

1. **`project.html`** — `theme/theme.css`를 싣는 `<link>` 줄 바로 아래에 한 줄:
   ```html
   <link rel="stylesheet" href="theme/deck-polish.css" />
   ```
2. **`assets/project-deck.js`** — 카드 안 문단 나누기. `const txt = …` 줄 아래에
   ```js
   const paras = (t) => String(t).split(/\n\s*\n/).map((x) => `<p>${inline(x.trim())}</p>`).join('');
   ```
   를 두고, `ncards`와 `flowHtml` 안의 `<p>${inline(txt(pt))}</p>`를 `${paras(txt(pt))}`로 바꿉니다.
   이것이 없으면 카드 글 안의 빈 줄이 한 문단으로 붙어 보일 뿐, 내용은 그대로입니다.
3. **프로젝트 표시(슬라이드 표지)** — `theme/project-marks.js`가 띠·AI 사용 여부를 정합니다.
   - `project.html`: `assets/deck-kit.js`를 싣는 줄 위에 `<script src="theme/project-marks.js"></script>`
   - `assets/deck-common.js`의 `projectIntro`: `const head = …` 줄 앞에 `pm`·`badges`를 만들고, `head`의 tagline 뒤에 `${badges}`를 넣습니다.
   이것이 없으면 슬라이드 표지에 표시만 빠집니다(포트폴리오 페이지는 그대로 보임).
4. **인쇄용 페이지(제출 PDF)** — `index.html`과 `assets/portfolio.js`
   - `index.html`: `theme/theme.css` 줄 아래에 `<link rel="stylesheet" href="theme/page-polish.css" />`, `assets/common.js` 줄 위에 `<script src="theme/project-marks.js"></script>`
   - `assets/portfolio.js`의 `projectIntro`: tagline 뒤에 `pm`·`badges`(슬라이드 표지와 같은 모양, 클래스 `pm-badges`)
   - `features`: `slice(0, 4)` → `slice(0, 6)` (기능 소개를 5개까지 싣기), 기능 설명 `<p>`에 `class="fl-lead"`
5. **그림 표시** — `theme/figure-marks.js`가 그림 뒤 주석 `<!-- pf-marks "낱말":x,y,w,h … -->`를 읽어 그림의 강조할 곳에 테두리를 그리고, 그 페이지 글 속 같은 낱말에 같은 색을 칠해 짝짓습니다(이름이 숫자면 다음 번호 목록과 짝. 쓰는 법은 그 파일 머리 주석). 슬라이드에서는 `PF.inline`도 감쌉니다(번호 짝의 캡션 짝 글).
   - `doc.html`·`project.html`: `assets/md-blocks.js`를 싣는 줄 바로 아래에 `<script src="theme/figure-marks.js"></script>`
   - `assets/`의 JS는 고치지 않습니다(`PF.md.parse`를 감싸고, 그려진 그림을 지켜보다가 덮개를 붙임).
   이것이 없으면 그림에 번호만 빠지고, 프로젝트 문서의 번호 목록은 보통 목록으로 보입니다.
   좌표 따기: `doc.html?p=<폴더>&pf-mark=1`. 목록 글에 `~`를 쓰면 GFM 취소선이 되니 피합니다.
6. **다이어그램 ELK 배치** — `theme/mermaid-elk.js`가 `@mermaid-js/layout-elk@0.1.7`을 불러와 flowchart·stateDiagram을 ELK로 배치합니다(선이 직각으로 정리되고 덜 겹침).
   - `doc.html`·`project.html`: mermaid CDN `<script>` 줄 바로 아래에 `<script src="theme/mermaid-elk.js"></script>`
   - `assets/`의 JS는 고치지 않습니다(`mermaid.initialize`·`render`·`run`을 감쌈). ELK를 받지 못하면 기본 배치로 그립니다.
   - 그림마다: 블록 안에 `%% pf-elk: model-order`(적은 순서대로 배치 — 순환이 많은 상태도) 또는 `%% pf-elk: off`(예전 배치).
   이것이 없으면 다이어그램이 예전 배치(dagre)로 돌아갈 뿐, 내용은 그대로입니다.
