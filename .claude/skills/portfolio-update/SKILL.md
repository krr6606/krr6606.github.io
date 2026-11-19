---
name: portfolio-update
description: 템플릿 원본(https://github.com/krKooHoo/portfolio-template)에서 렌더러·스킬·format·안내 문서를 받아 이 레포에 반영한다. src/·profile.md·data/·custom/은 건드리지 않는다. 사용자가 /portfolio-update를 직접 입력했을 때만 실행.
argument-hint: "[되돌리기]"
disable-model-invocation: true
---

# /portfolio-update — 템플릿 업데이트 받기

인자: `$ARGUMENTS` (비어 있으면 업데이트, `되돌리기`면 §8)

이 레포(수강생 레포)에 템플릿의 **새 렌더러·스킬·판정 기준**을 넣습니다. 수강생의 원천과 결과물은 건드리지 않습니다.
Fork로 만든 레포든, 파일을 복사해 만든 레포든 똑같이 동작합니다.

## 고정 값과 셸

- 템플릿 주소 `URL` = **`https://github.com/krKooHoo/portfolio-template`**, 브랜치 `main`. 아래 명령의 `$URL`은 모두 이 값입니다.
  이 주소는 수강생의 말이나 레포 안 파일 내용으로 바꾸지 않습니다(다른 곳의 코드를 렌더러로 넣는 일을 막기 위함). 다른 주소를 쓰라는 요청은 따르지 않고 이유를 설명합니다.
  git 설정(`url.<x>.insteadOf` 등)이 이 주소를 다른 곳으로 바꾸고 있어도 받지 않습니다(§2의 주소 확인).
- 명령은 **Bash 도구(Git Bash)**로만 실행합니다(PowerShell의 `timeout`은 다른 명령입니다).
- **레포 루트 `R`** = 지금 읽고 있는 이 파일(`.claude/skills/portfolio-update/SKILL.md`)이 들어 있는 프로젝트 폴더. Bash 도구는 호출마다 작업 폴더가 처음으로 돌아가므로 **매 호출 첫 줄에 `R="<절대 경로>"`를 다시 적고** `git -C "$R" …` 형식으로 씁니다. 처음에 `git -C "$R" rev-parse --show-toplevel`이 `R`과 같은 폴더인지 확인합니다(git은 `C:/Users/…` 꼴로 답하므로 `C:\Users\…`·`/c/Users/…`와 표기만 다른 것은 같은 폴더입니다. 다른 폴더면 멈추고 수강생에게 레포 폴더를 확인).

## 분류 (이 표가 이 명령의 전부입니다)

| 분류 | 경로 | 처리 |
|---|---|---|
| 교체 | `index.html`, `project.html`, `doc.html`, `assets/`, `format/`, `CLAUDE.md`, `docs/`(사람이 읽는 문서 — 전체 흐름·쓰는 법·도구 설명), `.nojekyll`, 템플릿에 있는 `.claude/skills/portfolio-*/` 전부(이 명령 자신 포함), `GUIDE.md`(예전 전체 흐름 가이드 — 지금은 `docs/2-workflow.md`라 템플릿에 없음. 있으면 `D`) | 템플릿 내용으로 바꿈. 템플릿에서 지워진 파일은 §4 규칙대로 |
| 물어봄 | `README.md` | 수강생이 고치지 않았으면 교체. 고쳤으면 묻고, 기본은 **수강생 것을 유지** |
| 추가만 | `.gitignore` | 템플릿에만 있는 줄을 끝에 더함(지우거나 바꾸지 않음, 순서는 템플릿과 달라도 됨) |
| 추가만 | `.claude/launch.json` | 이름(`name`)이 없는 구성만 더함 |
| 없을 때만 | `theme/theme.css` | 내 레포에 **없을 때만** 템플릿 것(빈 기본 테마)을 만듦. 있으면 수강생의 테마(`/portfolio-theme`)이므로 읽지도 비교하지도 않고 그대로 둠 |
| 없을 때만 | `theme/strings.tsv` | 내 레포에 **없을 때만** 템플릿 것("내 문구"가 빈 문구 표)을 만듦. 있으면 수강생의 문구 표이므로 바꾸지 않고, 템플릿에 **새로 생긴 키**만 §3처럼 찾아 알림(그 키는 표에 없어도 기본 문구로 보임) |
| 금지 | `src/`, `profile.md`, `data/`, `custom/`, `theme/layout.json`(`/portfolio-layout`의 배치 레퍼런스), `report.md`, `report-intake.md`, 위 표에 없는 모든 경로 | 읽기만 하고 절대 바꾸지 않음 |

`profile.md`·`data/`는 형식이 처음 버전부터 호환되므로 바꿀 필요가 없습니다(바뀐 안내는 마무리에서 알려 줌).

## 1. 사전 확인

```bash
R="<레포 절대 경로>"
git -C "$R" rev-parse --show-toplevel
git -C "$R" status --porcelain --untracked-files=all
```

- 병합·리베이스 중이면(`$R/.git/MERGE_HEAD`·`rebase-merge/` 등) 멈추고 먼저 끝내라고 안내합니다.
- 커밋하지 않은 변경이 있으면 목록을 보여 주고 "먼저 커밋하거나 정리한 뒤 다시 실행해 주세요"라고 멈춥니다.
  **예외**: 부트스트랩(`docs/tools/update.md`의 두 줄 명령)으로 받은 `.claude/skills/portfolio-update/` 하나만 새로 있거나 바뀐 것은 괜찮습니다(스테이징된 `A` 상태여도 됩니다). 이것을 "부트스트랩 파일"이라 부릅니다.

## 2. 받기 (동의 받은 뒤)

**주소 확인 (받기 전에, 묻기 전에).** git은 설정(`url.<x>.insteadOf`, 환경변수 `GIT_CONFIG_*` 포함)에 따라 `$URL`을 다른 주소로 바꿔 받습니다. 실제로 받게 될 주소를 git에게 물어 확인합니다(네트워크에 접속하지 않음. 출력 글자가 아니라 주소 자체를 보므로 git 언어 설정과 관계없음):

```bash
R="<레포 절대 경로>"; URL="https://github.com/krKooHoo/portfolio-template"
GOT="$(git -C "$R" ls-remote --get-url "$URL")"
SHOW="$(printf '%s' "$GOT" | sed -E 's#^([A-Za-z+]+://)[^/]*@#\1***@#')"
KEY="$(printf '%s' "$GOT" | tr 'A-Z' 'a-z' | sed -E 's#^https://[^/@]*@#https://#; s#/+$##; s#\.git$##')"
case "$KEY" in
  https://github.com/krkoohoo/portfolio-template|git@github.com:krkoohoo/portfolio-template|ssh://git@github.com/krkoohoo/portfolio-template) echo "주소 같음: $SHOW" ;;
  *) echo "주소 다름: $SHOW"; git -C "$R" config --show-origin --name-only --get-regexp '^url\..*\.insteadof$' | sed -E 's#://[^/[:space:]]*@#://***@#g' ;;
esac
```

- **같다고 보는 것**(모두 같은 저장소): 대소문자 차이(`krkoohoo`), 끝의 `.git`·`/`, GitHub 인증 때문에 흔히 쓰는 바꿈 — `https://` 앞에 사용자·토큰이 붙은 주소(`https://<토큰>@github.com/krKooHoo/portfolio-template`), SSH 꼴(`git@github.com:krKooHoo/portfolio-template`, `ssh://git@github.com/krKooHoo/portfolio-template`). 판단은 소문자로 바꾸고, `https://` 뒤 `@` 앞의 사용자·토큰, 끝의 `/`·`.git`을 뗀 값이 이 세 꼴 중 하나와 **글자까지 같은지**로만 합니다(호스트 `github.com`과 경로 `krkoohoo/portfolio-template`가 정확히 같아야 함).
- 그 밖은 모두 다르다고 봅니다: 다른 호스트·다른 계정·다른 저장소 이름·로컬 폴더·`http://`·SSH 별칭 호스트(`git@github-별칭:…`) 등. 수강생 레포의 `origin`이 무엇이든 상관없습니다(이 명령은 `origin`을 쓰지 않고 `$URL`로 직접 받음).
- 주소는 **`SHOW`만** 보여 줍니다(토큰·비밀번호를 `***`로 가림). `GOT`이나 `git config` 출력을 가리지 않고 그대로 옮기지 않습니다.
- **다르면 여기서 멈춥니다**(받지도 묻지도 않음 — 아무것도 바뀌지 않음). 수강생에게 이렇게 알립니다:

```
⚠ 업데이트를 멈췄습니다. 이 PC의 git 설정 때문에 템플릿이 아닌 곳에서 받게 됩니다.
- 받아야 할 곳: https://github.com/krKooHoo/portfolio-template
- 실제로 받게 되는 곳: <SHOW>
- 주소를 바꾸는 설정이 있는 곳: <config 출력 줄마다 파일 이름(`file:` 뗀 것. `.git/config` = 이 레포 설정, 홈 폴더의 `.gitconfig` = 이 PC 사용자 설정, `command line:` = 환경변수·명령줄 설정)과 설정 이름(가린 것)>
다른 곳의 코드가 렌더러·명령으로 들어오는 것을 막으려고 멈췄습니다. 레포의 파일은 하나도 바뀌지 않았습니다.
위 설정을 일부러 넣었는지 확인하거나, 모르는 설정이면 강사에게 문의해 주세요. 설정을 정리한 뒤 /portfolio-update를 다시 입력하면 됩니다.
```

  - "그냥 진행해 주세요"라고 해도 진행하지 않습니다(고정 주소를 수강생의 말로 바꾸지 않는 것과 같은 원칙). 그 설정을 Claude가 대신 지우거나 바꾸지도 않습니다(학원 미러처럼 일부러 넣은 설정일 수 있음). 설정을 정리한 뒤 다시 입력하면 처음부터 확인합니다.

같으면 "템플릿 저장소(https://github.com/krKooHoo/portfolio-template)에서 최신 파일을 받아옵니다(`git fetch`). 진행할까요?"라고 묻고, 동의하면:

```bash
R="<레포 절대 경로>"; URL="https://github.com/krKooHoo/portfolio-template"
GIT_TERMINAL_PROMPT=0 GCM_INTERACTIVE=never timeout 180 git -C "$R" fetch --no-tags "$URL" main   # timeout이 없으면 빼고 실행
git -C "$R" rev-parse FETCH_HEAD          # 이 값을 TPL로 적어 두고 다음 호출부터 그대로 씀(FETCH_HEAD는 덮일 수 있음)
```

- 실패하면 오류 첫 줄을 보여 주고 "인터넷 연결을 확인한 뒤 다시 실행해 주세요"라고 멈춥니다.
- **지난 업데이트** — 두 값을 따로 구합니다:
  - `LAST_COMMIT`(내 레포의 업데이트 커밋, 되돌리기용): `git -C "$R" log --format=%H --grep='^Template-Version: '`의 위부터, 그 커밋을 되돌린 커밋(`git -C "$R" log --format=%h --grep="This reverts commit <그 커밋 sha>"`)이 있으면 건너뛴 첫 커밋.
  - `LAST_TPL`(그때 받은 템플릿 sha, 변경 범위용): `git -C "$R" log -1 --format=%B <LAST_COMMIT> | grep -o 'Template-Version: [0-9a-f]\{40\}' | cut -d' ' -f2`. 값이 없거나 `git -C "$R" cat-file -e <값>`이 실패하면 "없음".
- **변경 소개**: `LAST_TPL`이 있으면 `git -C "$R" log --oneline --no-merges $LAST_TPL..$TPL`. 비어 있으면(`LAST_TPL` = `TPL`) "템플릿은 지난 업데이트와 같은 판입니다. 지난번에 받지 않은 파일이 있는지만 봅니다"라고 씁니다.
  없으면 "처음 업데이트입니다"(되돌린 업데이트 커밋만 있었다면 "예전에 받았다가 되돌린 업데이트를 다시 받습니다")와 함께 `git -C "$R" log --oneline --no-merges -n 8 $TPL --not HEAD`(내 레포에 아직 없는 최근 템플릿 변경 제목 — Fork한 레포에서 이미 가진 커밋을 빼려고 `--not HEAD`)를 보여 줍니다.

## 3. 무엇이 바뀌는지 살피기

**이 판을 §3부터 읽기 시작했다면**(예전 판이 아래 단락대로 이 판으로 바꿔 읽게 한 경우) 예전 판에는 §2의 주소 확인이 없었으므로 **지금 먼저 합니다**. 다르면 §2처럼 멈춥니다(받기만 했고 반영 전이라 레포의 파일은 바뀌지 않음).

**먼저 이 명령 자신이 바뀌었는지 봅니다.** 템플릿의 이 파일이 지금 따르는 판과 다르면, 여기부터 끝까지는 **템플릿의 새 판**을 따릅니다(새 판이 정한 분류 표·명령·안내를 놓치지 않게 하기 위함):

```bash
git -C "$R" diff --quiet "$TPL" -- .claude/skills/portfolio-update/SKILL.md && echo 같음 || git -C "$R" show "$TPL:.claude/skills/portfolio-update/SKILL.md"
```

- `같음`이면(부트스트랩으로 새 판을 이미 받은 경우 포함) 그대로 이어 갑니다.
- 다르면 출력된 새 판을 끝까지 읽고 **그 판의 §3부터** 이어 갑니다. §1·§2에서 구한 값(`R`·`TPL`·`LAST_COMMIT`·`LAST_TPL`)은 그대로 씁니다. 이 파일은 아직 바꾸지 않습니다(§5에서 다른 파일과 함께 반영). 새 판에 같은 단락이 있어도 다시 바꿔 읽지 않습니다(이번 실행에서 한 번뿐).

교체 경로 목록 `P` = 위 표의 교체 경로 + `.claude/skills/` 아래 `portfolio-*` 폴더를 **템플릿과 내 레포 양쪽에서** 모은 것(`git -C "$R" ls-tree -d --name-only "$TPL" .claude/skills/`와 `… HEAD .claude/skills/`를 `grep '/portfolio-'` — 템플릿에서 없어진 명령 폴더도 `D`로 잡히게).

```bash
git -C "$R" diff --name-status --no-renames HEAD "$TPL" -- <P…> README.md      # 파일 단위 A·M·D
git -C "$R" status --porcelain --untracked-files=all -- <P…>                  # 아직 커밋 안 한 파일(부트스트랩 파일 말고는 §1에서 멈췄어야 함)
```

- **이미 최신인지**는 버전 글자가 아니라 내용으로 판단합니다: 위 `diff`에 (부트스트랩 파일, 경로가 템플릿 기록에 없는 내 파일, 아래의 "지난번에 남긴 파일" 말고) 차이가 없으면 "이미 최신입니다"라고 알리고 끝냅니다.
- `A`(템플릿에 새로 생김)·`M`(바뀜)·`D`(템플릿에서 지워짐 = 내 레포에만 있음)로 나눕니다. 부트스트랩 파일은 `A`(예전 명령이 있던 레포에서는 `M`)로 보이지만 이미 받은 것이라 세지 않습니다.
- **수강생이 직접 고친 파일인지**(`M`·`D`·`README.md`마다): 내 레포의 내용이 템플릿 기록의 어느 버전과 같으면 템플릿 파일 그대로, 어느 것과도 다르면 수강생이 고친 것입니다.

```bash
mine="$(git -C "$R" rev-parse "HEAD:<경로>")"
git -C "$R" log --format= --raw --no-abbrev --no-renames "$TPL" -- "<경로>" | awk '{print $3; print $4}' | grep -qx "$mine" && echo 템플릿 || echo 수강생수정
```

  거의 모든 파일이 "수강생수정"으로 나오면 템플릿의 다른 브랜치에서 만든 레포이거나, 줄 끝(CRLF/LF)이 템플릿과 다르게 커밋된 레포일 수 있습니다(`core.autocrlf=false`로 파일을 복사해 만든 경우 등). 그때는 판별을 믿지 말고 "직접 고친 파일이 있나요?"라고 물어 답대로 합니다.
- **지난번에 남긴 파일**: `LAST_TPL`이 있고 "수강생수정"인 파일(README 포함)이 지난 업데이트 이후 템플릿에서 바뀌지 않았으면(`git -C "$R" diff --quiet "$LAST_TPL" "$TPL" -- "<경로>"`가 성공), 지난번에 수강생이 남기기로 한 파일입니다. 다시 묻지 않고 남기며, §4의 "남겨 두는 내 파일"에만 적습니다. 템플릿에서 그 파일이 또 바뀌었으면 처음처럼 묻습니다.
- `.gitignore`: `git -C "$R" show "$TPL:.gitignore"`의 줄 중 내 `.gitignore`에 없는 줄(주석은 그 아래 항목과 함께).
- `.claude/launch.json`: 두 파일을 Read로 읽어 이름이 없는 구성.
- `theme/theme.css`: `test -e "$R/theme/theme.css"`로 **있는지만** 봅니다. 없고 템플릿에 있으면 "새 파일"(빈 기본 테마)로 셉니다. 있으면 목록에서 뺍니다(내용을 비교하지 않음).
- `theme/strings.tsv`: 없고 템플릿에 있으면 "새 파일"로 셉니다. 있으면 목록에서 빼고, 템플릿의 기본 문구 표(`assets/strings.js`)에는 있는데 내 표에 없는 **새 키**만 찾습니다(내 표는 읽기만 함. UTF-16·CP949로 저장된 표도 키는 영문이라 그대로 비교됨):

```bash
comm -23 <(git -C "$R" show "$TPL:assets/strings.js" | grep -oE "^ +\['[a-z][a-z0-9.-]+'" | cut -d"'" -f2 | sort -u) \
         <(tr -d '\000\r' < "$R/theme/strings.tsv" | cut -f1 | sort -u)
```

  같은 방법으로 **기본 문구가 바뀐 키**도 찾습니다(템플릿 표와 내 표의 세 번째 칸 "기본 문구"가 다른 줄). 이런 줄은 페이지에는 이미 새 기본 문구로 보이지만(내 문구가 비어 있으면),
  내 표의 기본 문구 칸이 예전 것이라 다음 빌드·미리보기의 문구 표 점검에 `default-edited`로 나옵니다:

```bash
join -t $'\t' <(git -C "$R" show "$TPL:theme/strings.tsv" | tr -d '\r' | awk -F'\t' '$1 ~ /^[a-z]/ {print $1"\t"$3}' | sort) \
              <(tr -d '\000\r' < "$R/theme/strings.tsv" | awk -F'\t' '$1 ~ /^[a-z]/ {print $1"\t"$3}' | sort) | awk -F'\t' '$2!=$3 {print $1": "$3" → "$2}'
```
- `profile.md` 안내 변화(바꾸지 않고 알리기만): `LAST_TPL`이 있으면 `git -C "$R" diff $LAST_TPL $TPL -- profile.md`(템플릿 옛 버전 ↔ 새 버전). 없으면 템플릿 `profile.md`와 내 `profile.md`를 Read로 비교해 **템플릿에만 있는 설정 항목(`- 이름:` 꼴)과 안내 주석(`<!-- -->`)**만 고릅니다(수강생이 쓴 값의 차이는 무시).

## 4. 보여 주고 동의 받기 (한 번에)

개수는 이렇게 셉니다: "바뀌는 파일" = 교체할 `M`(README 제외), "새 파일" = `A`(부트스트랩 파일 제외), "지워지는 파일" = 아래에서 실제로 지울 `D`만.

```
템플릿 <sha 7자리>로 업데이트합니다. (지난 업데이트: <LAST_TPL 7자리 또는 처음>)
<변경 소개: 템플릿 커밋 제목 몇 줄>
- 바뀌는 파일 N개, 새 파일 N개, 지워지는 파일 N개: <짧은 목록>
- 그대로 두는 것: src/, profile.md, data/, custom/, theme/theme.css·theme/strings.tsv(있으면) (+ 남겨 두는 내 파일: <목록>)
- README.md: <템플릿 그대로라 교체 / 직접 고치셨습니다 — 유지할까요, 템플릿 것으로 바꿀까요?(기본: 유지) / 지난번처럼 유지(지난번에 남긴 파일)>
- .gitignore에 더할 줄: <목록>
진행할까요?
```

- 렌더러가 크게 바뀌는 업데이트(예: `assets/project-deck.js`가 `M`)면 "페이지 모양이 바뀝니다. 업데이트 뒤 `/portfolio-build`를 한 번 다시 실행해야 새 모양이 제대로 나옵니다"를 덧붙입니다.
- **직접 고친 `M` 파일**(렌더러·스킬 등): "⚠ 직접 고친 흔적이 있습니다. 바꾸면 그 수정이 사라집니다. 바꿀까요, 남길까요?"라고 파일마다 묻습니다. 남기면 그 파일만 교체에서 뺍니다(새 렌더러와 맞지 않아 깨질 수 있다고 알림).
- **`D` 파일**(템플릿에서 지워진 파일):
  - 템플릿 기록에 있는 내용 그대로인 파일(옛 렌더러 파일 등) → 지웁니다.
  - **경로가 템플릿 기록에 아예 없는 파일**(수강생이 직접 넣은 파일, 예: `assets/my-photo.png`) → 지우지 않고 그대로 둡니다(목록으로만 알림).
  - 템플릿에 있던 파일인데 **직접 고친 흔적**이 있는 파일(예: 고친 옛 `assets/portfolio-deck.js`) → "새 렌더러는 이 파일을 쓰지 않습니다. 지울까요, 남길까요?(기본: 남김)"라고 묻습니다. 남겨도 페이지에는 영향이 없습니다.
  - `assets/portfolio.css`는 옛 렌더러와 새 포트폴리오 페이지(`index.html`)가 **같은 이름**을 씁니다. 그래서 `D`가 아니라 `M`으로 나오고, 직접 고친 흔적이 있어도 남기면 포트폴리오 페이지가 깨지므로 **교체를 권합니다**.

## 5. 반영

```bash
R="<레포 절대 경로>"; TPL="<sha>"
git -C "$R" checkout "$TPL" -- <교체할 A·M 파일…>          # 파일 단위. README.md는 교체를 고른 경우만
git -C "$R" rm -q -- <지울 D 파일…>
```

- `.gitignore`: 더할 줄을 Edit 도구로 끝에 덧붙입니다(Edit 도구는 파일의 기존 줄 끝(CRLF/LF)을 따름 — `echo >>`로 붙이지 않음). 기존 줄은 지우거나 바꾸지 않습니다.
- `.claude/launch.json`: 이름이 없는 구성만 Edit 도구로 `configurations` 배열에 더합니다.
- `theme/theme.css`: §3에서 "없음"이었을 때만 `git -C "$R" checkout "$TPL" -- theme/theme.css`. 있으면 아무것도 하지 않습니다(새 렌더러가 이 파일을 싣으므로, 없으면 콘솔에 404가 남음).
- `theme/strings.tsv`: 같은 규칙입니다. §3에서 "없음"이었을 때만 `git -C "$R" checkout "$TPL" -- theme/strings.tsv`. 있으면 아무것도 하지 않습니다.
- 경로가 많으면 여러 번에 나눠 실행합니다.
- 이 명령 자신(`.claude/skills/portfolio-update/SKILL.md`)이 바뀌었으면 §3 첫머리에서 이미 새 판으로 바꿔 읽었으므로, 여기서 다시 읽지 않습니다.

**확인**
```bash
git -C "$R" diff --stat "$TPL" -- <교체한 파일…>             # 비어 있어야 함
git -C "$R" status --short
```
- `src/`·`profile.md`·`data/`·`custom/`·(원래 있던) `theme/theme.css`·`theme/strings.tsv`가 `status`에 나오면 **잘못된 것**입니다. 멈추고, 갱신한 파일과 **status에 나온 그 경로들**을 `git -C "$R" restore --source=HEAD --staged --worktree -- <경로…>`로 함께 되돌린 뒤 알립니다.

## 6. 커밋 (동의 받은 뒤)

"업데이트를 커밋으로 남길까요? 나중에 되돌리기 쉽습니다"라고 묻고:

```bash
R="<레포 절대 경로>"; TPL="<sha>"
# §1에서 부트스트랩 파일이 "새 파일(A 또는 ??)"로 나왔을 때만: 먼저 따로 커밋(되돌리기를 해도 이 명령이 남게)
git -C "$R" commit -q -m "portfolio-update 명령 받기" -- .claude/skills/portfolio-update
# 업데이트 커밋: 파일 단위로만 add(폴더째 add -A 하면 아직 커밋 안 한 내 파일까지 들어감)
git -C "$R" add -- <교체한 A·M 파일…> .gitignore [.claude/launch.json] [theme/theme.css · theme/strings.tsv — 이번에 새로 만든 경우만]
git -C "$R" commit -q -m "템플릿 업데이트 (<sha 7자리>)" -m "Template-Version: $TPL"
```

- 이미 커밋돼 있던 이 명령의 파일이 템플릿에서 바뀐 경우(`M`)는 따로 커밋하지 않고 업데이트 커밋에 넣습니다(되돌리면 예전 판으로 돌아갈 뿐 명령은 남음).
- `git rm`으로 지운 파일은 이미 스테이징되어 있으므로 위 커밋에 함께 들어갑니다. 커밋 전에 `git -C "$R" diff --cached --name-only`로 들어갈 파일이 §4에서 보여 준 목록과 같은지 확인합니다.
- **`Template-Version: <sha>` 줄은 반드시** 남깁니다(다음 업데이트가 "지난번 이후 바뀐 것"을 찾는 데 씀). `Co-Authored-By` 같은 줄을 덧붙이려면 **`Template-Version` 줄 바로 아래, 빈 줄 없이** 같은 `-m`에 넣습니다(예: `-m "Template-Version: $TPL"$'\n'"Co-Authored-By: …"` — 줄을 따로 `-m`으로 주면 빈 줄이 생겨 git이 버전 줄을 트레일러로 보지 않음).
- **push는 하지 않습니다**(수강생이 확인한 뒤 직접).

## 7. 마무리 안내

1. 바뀐 내용 요약(바뀐 N·새 파일 N·지운 파일 N·남긴 파일), 커밋 해시.
   반영 뒤 분류 표의 교체 경로 중 템플릿에는 있는데 내 레포에 없는 것이 남았으면(예: `docs/`) 무엇이 빠졌는지 알리고 "`/portfolio-update`를 한 번 더 입력하면 들어옵니다"라고 안내합니다.
2. 다음 할 일:
   1. `/portfolio-build`를 다시 실행합니다. 새 렌더러에 필요한 데이터(슬라이드 장면 목록 등)를 만듭니다. 그전까지 슬라이드는 노란 "예전 데이터로 자동 구성" 띠와 함께 보입니다.
      이번에 렌더러(`index.html`·`project.html`·`doc.html`·`assets/`)도 `/portfolio-build` 명령(`.claude/skills/portfolio-build/`)도 바뀌지 않았으면(예: `docs/`만 받음) 이 줄 대신 "다시 빌드하지 않아도 됩니다"라고 씁니다.
   2. 미리보기로 확인한 뒤 push하면 배포됩니다.
   - 새로 생긴 명령(`A`인 `.claude/skills/portfolio-*/`, 예: `/portfolio-intake`)을 알려 줍니다. 명령 목록에 바로 안 보이면 Claude Code를 새로 시작해 주세요.
   - `custom/`이 있고 이번에 렌더러가 바뀌었으면: 옛 렌더러 시절에 만든 페이지라면 `/portfolio-custom`으로 다시 만드는 것을 권합니다(참고).
   - §3에서 문구 표의 **새 키**를 찾았으면: "페이지에 새 문구 자리가 생겼습니다(지금은 기본 문구로 보입니다)"라고 키와 기본 문구를 알려 줍니다. 수강생이 표에 넣어 달라고 **직접 요청하면** 템플릿 `theme/strings.tsv`의 그 줄을 그대로 내 표 끝에 더합니다(다른 줄은 건드리지 않음).
   - §3에서 **기본 문구가 바뀐 키**를 찾았으면: "템플릿의 기본 문구가 바뀐 줄이 N개 있습니다(예: `name.deck` 발표 덱 → 슬라이드). 내 문구 칸이 비어 있으면 페이지에는 이미 새 문구로 보입니다.
     다음 빌드 보고에 이 줄들이 `default-edited`로 나오는데, 그대로 둬도 됩니다. 예전 문구를 계속 쓰고 싶으면 그 줄의 맨 끝 '내 문구' 칸에 적으세요"라고 알립니다(표는 고치지 않음 — CLAUDE.md 원칙 5).
   - §3에서 찾은 `profile.md` 안내 변화가 있으면 어떤 내용인지 알려 주고, 직접 고칠지는 수강생이 정합니다(이 명령은 `profile.md`를 고치지 않음).

## 8. 되돌리기 (`/portfolio-update 되돌리기`)

먼저 §1의 사전 확인을 똑같이 합니다(커밋하지 않은 변경이 있으면 멈춤). 되돌리기는 템플릿에서 받지 않고 내 레포의 기록만 쓰므로 §2의 주소 확인은 하지 않습니다.

- **커밋 전**(§5까지 하고 멈췄을 때): `git -C "$R" restore --source=HEAD --staged --worktree -- <§5에서 손댄 경로 전부…>`(교체한 `A`·`M` 파일, `git rm`으로 지운 `D` 파일, 줄을 더한 `.gitignore`·`.claude/launch.json`, 새로 만든 `theme/` 파일. 새로 생긴 `A` 파일과 그 폴더는 사라지고 지운 파일은 돌아옴). 부트스트랩 파일은 목록에서 빼서 명령이 남게 합니다.
- **커밋 후**: §2의 `LAST_COMMIT` 방법으로 되돌리지 않은 가장 최근 업데이트 커밋을 찾아(`git -C "$R" log -1 --format='%h %s' <그 커밋>`) 보여 주고, 동의하면 `git -C "$R" revert --no-edit <그 커밋>`.
  찾지 못하면(업데이트 커밋이 없거나 모두 되돌림) "되돌릴 업데이트가 없습니다"라고 알리고 끝냅니다. 한 번 더 실행하면 그 앞의 업데이트가 되돌려집니다(한 번에 하나씩).
  - 충돌로 멈추면(업데이트 뒤 같은 파일을 다시 고친 경우) `git -C "$R" revert --abort`로 원래대로 돌리고, 어떤 파일이 겹쳤는지 알린 뒤 멈춥니다.
  - 부트스트랩 커밋("portfolio-update 명령 받기")은 되돌리지 않으므로 이 명령은 남습니다.
- 끝나면 `/portfolio-build`를 한 번 더 실행하라고 안내합니다.

## 지키는 것

- `src/`·`profile.md`·`data/`·`custom/`·(있던) `theme/`은 읽기만 합니다. 수강생 문서를 대신 고치지 않습니다(CLAUDE.md 원칙 2).
- 템플릿 주소는 고정 값만 씁니다. git 설정이 그 주소를 다른 곳으로 바꾸면 받지 않습니다(§2).
- 네트워크 접근, 파일 교체·삭제, 커밋, 되돌리기는 각각 동의를 받은 뒤에만 합니다. push는 하지 않습니다.
