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
