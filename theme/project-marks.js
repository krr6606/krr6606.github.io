/* 프로젝트 표시 (theme/project-marks.js) — 포트폴리오 페이지(custom/)와 슬라이드 표지가 함께 읽습니다.
   ai: true = AI 활용, false = AI 없이 직접 작성 · mark: 눈에 띄게 붙일 띠 글(없으면 생략) · note: 출처 등 꼭 밝힐 말(옅은 칩)
   근거: 냥코 문서 첫머리 "AI를 쓰지 않은", 시장에 가면은 수강생이 밝힘, 하이브 퍼지 4.2 AI와 일을 나눈 방식, 시장에 가면 팀 4인 · 서버 단독 */
window.PF_MARKS = {
  '1-nyanko-defense': { ai: false, note: '모작 · 그림·사운드는 원작(냥코 대전쟁)' },   // 문서 첫머리의 밝힘
  '2-sh-server': { ai: false, mark: '팀 프로젝트 · 서버 단독 담당' },
  '3-hive-purge': { ai: true, aiLabel: 'AI 활용 · Claude Code' },
};
