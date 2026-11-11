---
name: portfolio-layout
description: 수강생이 보여 준 웹페이지(주소 또는 스크린샷)에서 배치 골격(띠 순서·단 나눔·카드 격자·그림 위치·콘텐츠 폭)을 재서 내 포트폴리오의 절에 맞춘 뒤 theme/layout.json에 쓴다. 그다음 /portfolio-custom이 이 배치로 새 페이지를 그린다. 사용자가 "이 사이트처럼 배치해 줘", "이 레이아웃 따라 해 줘", "카드 3열로 보여 주는 이 페이지처럼"처럼 다른 페이지의 배치·구성·레이아웃을 따라 하고 싶다고 하거나 /portfolio-layout을 입력할 때 사용. 색·모양·글꼴만 바꾸는 요청은 /portfolio-theme.
argument-hint: "<레퍼런스 주소 | (스크린샷 첨부)> [덧붙일 요청] | 되돌리기"
---

# /portfolio-layout — 레퍼런스 화면에서 배치 골격 뽑기

인자: `$ARGUMENTS`

수강생이 보여 준 페이지의 **배치 골격**(위에서부터의 띠 순서, 띠마다 한 단·2단·카드 격자 같은 나눔, 그림이 놓인 쪽, 콘텐츠 폭)을 숫자로 재서,
내 포트폴리오의 절(머리·자기소개·기술 스택·프로젝트 소개·기능 소개·한눈에 보기·연락처)에 맞춘 뒤 **`theme/layout.json` 한 파일**에 씁니다.
그 파일은 `/portfolio-custom`(직접 생성 모드)이 읽어 `custom/index.html`을 그 배치로 그립니다.

- **쓰는 파일은 `theme/layout.json` 하나뿐입니다.** 렌더러(`index.html`·`project.html`·`doc.html`·`assets/`)·`src/`·`profile.md`·`data/`·`custom/`은 고치지 않습니다.
  포트폴리오 문서(`index.html`)와 제출 PDF의 배치는 그대로입니다(A4 인쇄와 발표 덱 측정이 고정 배치에 맞춰져 있음). 배치를 바꾼 페이지는 `custom/` 쪽에 따로 생깁니다.
- **글·그림·로고·회사/서비스 이름·주소는 가져오지 않습니다.** 측정 스크립트는 상자의 위치·크기·배경 유무·글자 크기 같은 숫자만 잽니다. 남의 페이지를 베낀 포트폴리오가 되면 안 됩니다.
- 색·모양·영문 글꼴은 이 명령이 아니라 `/portfolio-theme`입니다. 둘은 함께 써도 됩니다.

## 0. 되돌리기

`$ARGUMENTS`가 `되돌리기`면 `theme/layout.json`이 있는지 보고, "배치 레퍼런스를 지울까요? 다음에 `/portfolio-custom`을 실행하면 디자인 요청대로만 그립니다(지금 있는 `custom/index.html`은 그대로)"라고 묻습니다.
동의하면 지우고(`rm`), 없으면 "배치 레퍼런스가 없습니다"라고 알리고 끝냅니다.

## 1. 입력 확인

- **주소가 있음** → 브라우저 도구(Claude 데스크톱 앱의 브라우저 또는 Claude in Chrome)로 엽니다. 로그인·쿠키 동의·팝업은 누르지 않습니다(거절 버튼이 있으면 거절). 로그인해야 보이는 페이지는 재지 않습니다.
  브라우저 도구가 없으면 "그 페이지 화면을 위에서부터 2~3장 캡처해서 첨부해 주세요"라고 안내하고 멈춥니다.
- **스크린샷만 있음** → §2-B. 여러 장이면 위에서부터 이어진 한 페이지로 봅니다.
- **둘 다 없이 말로만**("첫 화면 크게, 프로젝트는 카드 3열로") → 재지 않고 그 말대로 §3의 표를 채웁니다. `mode`는 `"described"`.
- `data/portfolio.json`·`data/projects/*.json`이 있으면 읽어 둡니다(프로젝트 수, 대표 이미지·기능 화면이 있는지 — §3에서 씀). 없어도 진행하되 "`/portfolio-build` 뒤에 `/portfolio-custom`을 실행하세요"라고 마무리에 적습니다.

## 2. 재기

### 2-A. 주소 — 측정 스크립트

1. 화면 크기를 **1440×900**으로 맞춥니다(데스크톱 앱 브라우저: `resize_window` width 1440 · height 900). 넓은 화면 배치를 재기 위해서입니다.
2. 레퍼런스 페이지를 엽니다. 다 그려질 때까지 기다립니다.
3. [measure-layout.js](measure-layout.js)를 Read로 읽어 **내용 전체를 그대로** 페이지에서 실행합니다(JavaScript 도구). 마지막 식의 값이 결과입니다:
   `page`(콘텐츠 폭·머리 막대 고정·띠 수) · `bands[]`(띠마다 `role`·`layout`·`cols`·`rows`·`ratio`·`media`·`height`·`align`·`background`·`titleScale`) · `wireframe`(한 줄씩의 그림) · `notes`.
   스크립트는 한 번 끝까지 스크롤했다가 맨 위로 돌아온 뒤 잽니다(늦게 나타나는 요소를 깨우려고). 10초 안팎 걸립니다.
4. 첫 화면과 한두 번 스크롤한 화면을 스크린샷으로 보고 `bands`와 맞는지 확인합니다. `role`은 짐작이므로 화면을 보고 바로잡습니다
   (예: `text`로 나왔지만 실제로는 큰 인용 띠, `split`이지만 실제로는 글 + 작은 아이콘). 스크린샷은 레포에 저장하지 않습니다.
5. `notes`에 "띠를 2개 이상 찾지 못했습니다"가 있거나(한 화면짜리 앱·캔버스 페이지) 결과가 화면과 크게 다르면, 그 부분은 §2-B처럼 화면을 보고 정합니다(`mode`: `"measured+screenshot"`).
6. 끝나면 화면 크기를 되돌립니다(`resize_window` preset `desktop`).

### 2-B. 스크린샷 — 눈으로 어림

스크린샷을 위에서부터 보며 띠마다 같은 말로 적습니다: `layout`(`stack` 한 단 · `split` 2단 · `columns` 3단 이상 · `grid` 카드 격자 · `rows` 줄마다 같은 비율로 나뉜 목록),
`ratio`(5% 단위), `media`(그림 쪽), `align`, `background`(`same` · `tinted` · `image`), 첫 화면이면 `height`(화면 높이 배수 어림).
`wireframe`도 측정 결과와 같은 모양으로 직접 만듭니다. `mode`는 `"screenshot"`, 보고에 "스크린샷을 보고 어림한 값"이라고 적습니다.

## 3. 내 포트폴리오의 절에 맞추기

포트폴리오의 절과 순서는 **바꾸지 않습니다**(`format/04-layout-patterns.md` 「포트폴리오 문서 절」): 머리 → 자기소개 → 기술 스택 → 프로젝트마다 [프로젝트 소개 → 기능 소개] → 한눈에 보기 → 연락처.
레퍼런스의 띠 순서를 옮기는 것이 아니라, **내 절마다 어울리는 레퍼런스 띠의 배치를 빌려 옵니다.**

| 내 절 (`layout.json` 키) | 무엇이 들어가나 | 어울리는 레퍼런스 띠(제안) |
|---|---|---|
| `cover` 머리 | 이름·한 줄 정체성·연락처 버튼 | 첫 화면(`hero`) |
| `intro` 자기소개 | 2~3문장 글 | 글 중심 한 단(`text`) |
| `stack` 기술 스택 | 기술 이름 + 어디에 썼는지 + 근거 프로젝트 | 줄마다 나뉜 목록(`list`·`rows`), 작은 카드 격자(`cards`), 여러 단(`columns`) |
| `project` 프로젝트 소개 | 대표 이미지 + 정보 + 개발 의도 + 링크 | 그림 한쪽 + 글(`split` 그림 왼쪽/오른쪽) |
| `features` 기능 소개 | 기능 화면 + 번호 · 제목 · 설명 | 카드마다 그림(`cards`), 그림 + 목록(`split`), 그림이 있는 줄 목록(`rows` 그림 쪽) |
| `glance` 한눈에 보기 | 프로젝트 요약(이름·한 줄 소개·핵심 기술·역할·링크) | 그림 없는 카드(`cards`), 여러 단 줄 목록(`rows`) |
| `contact` 연락처 | 연락처 + 프로젝트별 링크 | 마지막 띠(`footer`) |
| `page.nav` 머리 막대 | 절 이동 | `nav`(화면 위 고정이면 `"sticky"`) |

- 한 레퍼런스 띠를 여러 절에 써도 됩니다(띠가 적은 페이지). 어울리는 띠가 없는 절은 넣지 않습니다(그 절은 디자인 요청대로 그림).
- 내 데이터에 없는 것을 요구하는 배치는 고르지 않습니다. 예: 기능 화면(`highlights[].image`)이 거의 없으면 `features`에 "카드마다 그림"을 고르지 않음. 대표 이미지가 없는 프로젝트가 있으면 `project`의 그림 칸은 그 프로젝트에서 빠진다고 알림.
- 같은 배치의 `split` 띠가 연달아 있고 그림 쪽이 번갈아 바뀌면(왼쪽 → 오른쪽 → 왼쪽) `project`에 `"alternate": true`.
- 포트폴리오에 해당하는 내용이 없는 띠(로고 줄, 후기, 가격표, 뉴스레터 가입 등)는 `unused`에 "무엇이었는지" 한 줄로 적습니다(이름·문구 없이 — "로고가 줄지어 있는 띠").

## 4. 보여 주고 확인받기

아래를 한 번에 보여 주고 "이렇게 쓸까요?"라고 **묻습니다**. 수강생이 **네**라고 한 뒤에만 씁니다. 바꾸고 싶은 칸이 있으면 고쳐서 다시 보여 줍니다.

````
레퍼런스 배치 (화면 1440×900에서 잰 값 · 띠 N개)
```
<wireframe 그대로>
```

| 내 절 | 빌려 올 배치 | 레퍼런스 띠 |
|---|---|---|
| 머리 | 한 단 · 큰 글자 · 화면 높이 0.9 · 배경색 다름 | 2 |
| 프로젝트 소개 | 2단 50:50 · 그림 왼쪽 · 프로젝트마다 좌우 번갈아 | 5 |
| … | | |

- 쓰지 않는 띠: 7(로고가 줄지어 있는 띠) …
- 가져오지 않는 것: 글·그림·로고·색. 색과 글꼴은 /portfolio-theme 또는 /portfolio-custom의 디자인 요청으로 정합니다.
- 포트폴리오 문서(index.html)와 제출 PDF는 바뀌지 않습니다. 이 배치는 /portfolio-custom으로 만드는 custom/index.html에 쓰입니다.
````

## 5. 쓰기 — `theme/layout.json`

Write로 씁니다(이미 있으면 덮어쓴다고 §4에서 알림 — git으로 되돌릴 수 있음).

```json
{
  "schemaVersion": 1,
  "madeAt": "YYYY-MM-DD",
  "reference": "<참고한 화면의 인상 한 줄 — 사이트·회사·서비스 이름, 주소, 문구는 적지 않음. 예: 첫 화면이 큰 게임 소개 페이지, 카드 3열>",
  "mode": "measured | measured+screenshot | screenshot | described",
  "viewport": "1440×900",
  "page": { "contentWidth": 1120, "nav": "sticky | static | none" },
  "sections": {
    "cover":    { "from": 2, "layout": "stack", "align": "left", "height": 0.9, "background": "tinted" },
    "intro":    { "from": 3, "layout": "stack", "align": "center" },
    "stack":    { "from": 4, "layout": "rows", "cols": 2, "ratio": [20, 80] },
    "project":  { "from": 5, "layout": "split", "ratio": [50, 50], "media": "left", "alternate": true },
    "features": { "from": 6, "layout": "grid", "cols": 3, "media": "top" },
    "glance":   { "from": 8, "layout": "rows", "cols": 3, "ratio": [25, 50, 25] },
    "contact":  { "from": 9, "layout": "split", "ratio": [40, 60] }
  },
  "unused": [{ "from": 7, "what": "로고가 줄지어 있는 띠" }],
  "wireframe": "<측정 결과의 wireframe 그대로(줄바꿈은 \\n)>"
}
```

| 필드 | 값 |
|---|---|
| `sections.<키>` | 키는 `cover`·`intro`·`stack`·`project`·`features`·`glance`·`contact` 중에서만. 없는 키 = 그 절은 디자인 요청대로 |
| `from` | `wireframe`의 띠 번호(어디서 빌려 왔는지 추적용). 말로만 정했으면 생략 |
| `layout` | `stack` · `split` · `columns` · `grid` · `rows` |
| `cols` · `ratio` | 단 수와 넓은 화면에서의 폭 비율(%, 합 100). `grid`는 `cols`만 |
| `media` | 그림이 놓이는 쪽: `left` · `right` · `top`(카드 위) · `none` |
| `align` · `background` · `height` | `left`·`center` / `same`·`tinted`(바탕과 다른 띠 색)·`image`(그러데이션 띠 — 그림 파일을 쓰라는 뜻이 아님) / 화면 높이 배수(`cover`만) |
| `alternate` | `project`에서 프로젝트마다 그림 쪽을 번갈아 |

쓴 뒤 확인:
```bash
python -c "import json,sys; d=json.load(open('theme/layout.json',encoding='utf-8')); bad=set(d['sections'])-{'cover','intro','stack','project','features','glance','contact'}; sys.exit(f'모르는 절: {bad}' if bad else 0)"   # 없으면 py
```
- [ ] JSON이 읽힌다. `sections`의 키가 위 7개 안에 있다
- [ ] `reference`·`unused`에 사이트·회사·서비스 이름, 주소, 페이지의 문구가 없다
- [ ] `theme/layout.json` 말고 바뀐 파일이 없다(`git status --short`)

## 6. 마무리 안내

```
배치 레퍼런스를 theme/layout.json에 썼습니다.

<§4의 표 — 내 절 · 빌려 올 배치>

다음 할 일
1. /portfolio-custom 을 직접 입력하면 이 배치로 custom/index.html을 만듭니다. 색·분위기는 뒤에 덧붙여 말하세요(예: /portfolio-custom 다크 톤, 강조색은 내 색).
2. 결과를 미리보기(…/custom/)로 보고, 마음에 들면 theme/layout.json과 custom/index.html을 커밋·push하세요.
- 포트폴리오 문서(index.html)와 제출 PDF는 그대로입니다. 배치를 바꾼 페이지는 …/custom/ 주소에 따로 생깁니다.
- 배치를 다시 뽑으려면 다른 주소나 스크린샷으로 /portfolio-layout, 지우려면 /portfolio-layout 되돌리기
```

- `mode`가 `screenshot`·`described`면 "스크린샷을 보고 어림한(말로 정한) 배치입니다"를 덧붙입니다.
- `data/`가 없었으면 1번 앞에 "먼저 `/portfolio-build`"를 넣습니다.

## 지키는 것

- 레퍼런스의 **글·그림·로고·아이콘·회사/서비스 이름·주소를 옮기지 않습니다**(`theme/layout.json` 포함). 배치의 숫자만 가져옵니다.
- 쓰는 파일은 `theme/layout.json` 하나입니다. 렌더러와 `custom/`은 고치지 않습니다(`custom/index.html`은 수강생이 `/portfolio-custom`을 직접 입력했을 때 만들어짐).
- 포트폴리오의 절과 순서, 내용은 바꾸지 않습니다. 데이터에 없는 내용을 채우려고 자리를 만들지 않습니다(CLAUDE.md 원칙 1).
- 로그인·결제·개인정보 입력이 필요한 페이지는 재지 않습니다. 쿠키 동의는 거절 쪽을 고릅니다.
- 커밋·push는 하지 않습니다(수강생이 확인한 뒤 직접).
