---
name: portfolio-custom
description: 직접 생성 모드 — data/의 포트폴리오 데이터로 Claude가 디자인을 새로 그린 단일 HTML 페이지(custom/index.html)를 만든다. theme/layout.json(/portfolio-layout이 뽑은 배치 레퍼런스)이 있으면 그 배치를 따른다. 사용자가 /portfolio-custom을 직접 입력했을 때만 실행.
argument-hint: "[디자인 요청 — 예: 다크 톤, 첫 화면은 이름만 크게]"
disable-model-invocation: true
---

# /portfolio-custom — 직접 생성 모드

디자인 요청: `$ARGUMENTS` (비어 있으면 "취업용답게 깔끔하고 절제된, 템플릿 같지 않은 디자인")

고정 렌더러(포트폴리오 문서 `index.html`)와 **같은 데이터**로, 겉모습만 새로 그린 페이지를 `custom/index.html`에 만듭니다.
고정 렌더러 파일(`index.html`·`project.html`·`doc.html`·`assets/`)은 **수정하지 않습니다.**

## 0. 준비

1. `format/05-custom-page-prompts.md`의 **생성 규칙**과 `format/04-layout-patterns.md`의 패턴 13종을 읽습니다.
2. `data/portfolio.json`과 `data/projects/*.json`을 읽습니다. **없으면 멈추고** `/portfolio-build`를 먼저 하라고 안내합니다.
3. 스키마는 [../portfolio-build/schema.md](../portfolio-build/schema.md) 참고.
4. `theme/layout.json`(배치 레퍼런스, `/portfolio-layout`이 씀 — 형식은 [../portfolio-layout/SKILL.md](../portfolio-layout/SKILL.md) §5)이 있으면 읽습니다. 있으면 §1-1을 따릅니다.
   JSON이 깨졌거나 `schemaVersion`이 1이 아니면 쓰지 않고 "배치 레퍼런스를 읽지 못했습니다 — `/portfolio-layout`으로 다시 뽑아 주세요"라고 보고에 적습니다.

## 1. 생성 규칙

- **단일 HTML 파일** `custom/index.html`. 설치·빌드 없음.
  - React 18 + ReactDOM은 CDN에서 **버전 고정** (`https://unpkg.com/react@18.3.1/umd/react.production.min.js` 등),
    JSX를 쓰면 `@babel/standalone@7.24.7`. 스타일은 Tailwind CDN(`https://cdn.tailwindcss.com`).
- **데이터는 `fetch('../data/portfolio.json')`로 읽어 렌더**합니다. JSON 내용을 HTML에 복사해 넣지 않습니다
  → 다음에 `/portfolio-build`만 다시 하면 이 페이지도 자동으로 최신이 됩니다.
- 내용은 데이터에 있는 것만. 문구를 새로 지어내지 않습니다 (섹션 제목·버튼 문구 같은 UI 텍스트는 예외).
  UI 텍스트는 문구 표 `theme/strings.tsv`를 따릅니다: 맨 끝 "내 문구" 칸이 있으면 그 문구, 비어 있으면 기본 문구(`assets/strings.js`의 같은 키). 표에 없는 자리만 새로 정합니다.
- 프로젝트 페이지는 새로 만들지 않고 `format/05` 규칙 3대로 링크합니다: 발표 덱 `../project.html?p=<id>`, 상세 기술문서 `../doc.html?p=<id>`.
- 이미지 경로: `../src/<id>/<cover 또는 screenshots[].src>` (상대경로).
- 강조색은 `settings.accent`를 기본으로 하되, 디자인 요청에 다른 색이 있으면 그 색.
- 반응형(모바일 375px에서 가로 스크롤 없음), 3색 이내, 여백·타이포그래피 중심.
- 링크가 빈 값(`""`)이면 그 버튼을 그리지 않습니다.

### 1-1. 배치 레퍼런스(`theme/layout.json`)가 있을 때

- **절과 순서는 포트폴리오 문서와 같습니다**: 머리(`cover`) → 자기소개(`intro`) → 기술 스택(`stack`) → 프로젝트마다 [프로젝트 소개(`project`) → 기능 소개(`features`)] → 한눈에 보기(`glance`) → 연락처(`contact`).
  `layout.json`은 레퍼런스의 띠 순서가 아니라 **절마다 빌려 올 배치**입니다.
- `sections`에 있는 절은 그 배치로 그립니다(`format/04`의 패턴 고르기보다 먼저):
  - `layout`: `stack` 한 단 · `split` 2단 · `columns` 3단 이상 · `grid` 카드 격자(`cols`열) · `rows` 줄마다 같은 비율로 나뉜 목록. `ratio`는 넓은 화면의 폭 비율(%).
  - `media`(그림 쪽): `project`는 대표 이미지(`cover`), `features`는 기능 화면(`highlights[].image`), 그 밖의 절은 그림을 새로 넣지 않습니다(데이터에 그림이 없는 절).
    그림이 없는 프로젝트·기능은 그 칸을 그리지 않고 글이 폭을 채웁니다 — **빈 자리·자리표시 그림을 만들지 않습니다.**
  - `alternate: true`(`project`): 프로젝트마다 그림 쪽을 왼쪽·오른쪽 번갈아.
  - `align`·`background`(`tinted` = 바탕과 다른 띠 색, `image` = 그러데이션 띠 — 그림 파일을 쓰지 않음)·`height`(`cover`의 화면 높이 배수, `min-height: <n>00vh`로).
  - `page.contentWidth`는 넓은 화면의 콘텐츠 최대 폭, `page.nav`가 `sticky`면 머리 막대를 화면 위에 고정합니다.
- `sections`에 없는 절은 디자인 요청대로 그립니다.
- **반응형이 먼저**입니다: 폭 760px 미만에서는 모든 배치를 한 열로(그림 → 글 순서), 가로 스크롤 없음.
- 디자인 요청(`$ARGUMENTS`)이 `layout.json`과 다르면 **요청이 먼저**입니다(예: "기능 소개는 카드 2열로").
- `layout.json`의 `reference`·`wireframe`은 참고용입니다. 레퍼런스의 글·그림·로고는 `layout.json`에 없으며, 페이지에 레퍼런스를 흉내 낸 문구·장식을 넣지 않습니다.

## 2. 확인과 보고

1. 로컬 서버(`python -m http.server 4600`, 없으면 `py -m ...`)로 `http://localhost:4600/custom/`을 열어
   콘솔 오류·깨진 이미지를 확인합니다 (브라우저 도구가 있으면 스크린샷까지).
2. 사용자에게 알립니다:
   - 주소와 어떤 디자인 결정을 했는지 3줄
   - `theme/layout.json`을 썼으면: 배치 레퍼런스를 따른 절과, 따르지 못한 절과 그 이유(예: "기능 화면이 없어 기능 소개는 글 카드로")
   - **"직접 생성 결과는 실행할 때마다 달라질 수 있습니다. 화면을 직접 확인하세요."**
   - 다듬고 싶으면 "방금 만든 페이지에서 [이 부분]을 [이렇게] 바꿔줘"처럼 이어서 말하면 된다는 안내
   - 메인으로 쓰려면 이력서 링크를 `.../custom/`으로 — 고정 렌더러는 백업으로 남겨 두기
