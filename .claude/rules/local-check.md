# 이 레포만의 방향과 점검 기준을 함께 읽기

포트폴리오 작업(프로젝트 문서·슬라이드·포트폴리오 페이지를 쓰기, 고치기, 리뷰하기, 방향 토론하기)을 할 때와
`/portfolio-check`, `/portfolio-build`, `/portfolio-edit`를 실행할 때는 아래 두 파일을 **먼저 읽습니다.**

- `private/local/portfolio-direction.md`: 수강생과 합의한 방향(정체성, 독자, 문서 구조, 작업 태도)과 프로젝트별 진행 상태. 새로 합의한 것이 생기면 이 파일을 고치고 `private/` 레포에 커밋합니다.
- `private/local/check-criteria.md`: `format/`에 더해 적용하는 점검 기준. 두 기준이 부딪히면 이 파일이 이깁니다. 점검 결과는 그 파일 §3대로 `report.md`의 `## 이 레포 기준 점검` 절에 씁니다.

## `private/` — 비공개 레포

이 레포는 GitHub Pages로 **공개**됩니다. 레포에 커밋한 파일은 누구나 주소로 열 수 있습니다.
그래서 방향 문서·점검 기준·세션 기록 같은 내부 자료는 **비공개 레포 `krr6606/portfolio-private`**에 두고,
이 레포 안의 `private/` 폴더로 받아 씁니다. `private/`은 `.gitignore`에 있어서 이 레포에는 커밋되지 않습니다.

- `private/local/`: 방향 문서·점검 기준
- `private/sources/<NN-프로젝트>/`: 세션 기록 같은 원자료 (예: `private/sources/3-hive-purge/`)

`private/`이 없는 컴퓨터에서는 먼저 받습니다(포트폴리오 레포 맨 위 폴더에서):

```powershell
git clone https://github.com/krr6606/portfolio-private.git private
```

내부 자료를 이 레포(공개)의 다른 폴더에 두지 않습니다. 고친 뒤에는 `private/` 안에서 커밋·푸시합니다.

(이 파일은 템플릿에 없어서 `/portfolio-update`가 덮어쓰지 않습니다. Claude의 메모리는 컴퓨터마다 따로라서, 컴퓨터를 옮겨도 남아야 하는 것은 `private/`에 둡니다.)
