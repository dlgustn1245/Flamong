// 성장 수치의 단일 출처. README 「성장 밸런스」와 같아야 한다. 클라이언트에는 계산 결과(growth)만 보낸다.

// 각 레벨이 시작되는 누적 애정도. 마지막 값은 완성 (Lv5 게이지 끝)
export const LEVEL_AT = [0, 5, 30, 65, 115, 175];
export const MAX_AFFECTION = LEVEL_AT[LEVEL_AT.length - 1];

export const HEARTS_START = 5;   // 가입 직후
export const HEARTS_DAILY = 5;   // 매일 KST 0시 충전량
export const HEARTS_MAX = 15;    // 저장 상한 (3일치)

export const EMOTION_AFFECTION = 2;
// 피로 · 스트레스 · 성취감 · 몰입 · 평온
export const EMOTIONS = ['tired', 'stress', 'achievement', 'focus', 'calm'];

export const GAME_AFFECTION = 2;   // 미니게임 하루 첫 판. 게임 종류와 상관없이 하루 한 번

export const POTION_AFFECTION = 10;
export const VISIT_PER_POTION = 7;   // 오늘의 감정을 고른 날 7일마다 1개. 연속이 아니라 누적
// 히든 미션. 한 번씩만 받는다. 순서가 Potion 씬 카드 순서다
export const HIDDEN_MISSIONS = ['first_complete', 'first_game', 'first_garden'];

/**
 * 방문 묘약 게이지. 받기 전에는 need 에서 멈춰 보이고, 그 뒤 방문은 쌓아 두었다가 받은 뒤 이어진다
 * @param {number} visitDays 오늘의 감정을 고른 날 수 @param {number} claimed 받은 방문 묘약 수
 */
export function visitStatus(visitDays, claimed) {
  return { progress: Math.min(VISIT_PER_POTION, visitDays - claimed * VISIT_PER_POTION), need: VISIT_PER_POTION };
}

const DAY_MS = 86_400_000;
const KST_MS = 9 * 3_600_000;

/** KST 기준 날짜 'YYYY-MM-DD'. 하루는 KST 0시에 바뀐다 @param {Date} d */
export function kstDate(d) {
  return new Date(d.getTime() + KST_MS).toISOString().slice(0, 10);
}

/**
 * hearts_date 이후 지난 날수만큼 충전한 하트.
 * 언제 계산해도 같은 값이라 읽을 때는 저장하지 않고, 하트를 쓸 때 (결과, 오늘)을 함께 저장한다.
 * @param {number} hearts @param {string} heartsDate @param {string} today
 */
export function rechargeHearts(hearts, heartsDate, today) {
  const days = Math.max(0, (Date.parse(today) - Date.parse(heartsDate)) / DAY_MS);
  return Math.min(HEARTS_MAX, hearts + HEARTS_DAILY * days);
}

/** 누적 애정도 → 레벨과 그 레벨 안의 진행값 @param {number} affection */
export function levelOf(affection) {
  let level = 1;
  while (level < LEVEL_AT.length - 1 && affection >= LEVEL_AT[level]) level++;
  return {
    level,
    levelProgress: affection - LEVEL_AT[level - 1],
    levelNeed: LEVEL_AT[level] - LEVEL_AT[level - 1],
    completed: affection >= MAX_AFFECTION,
  };
}

/** 응답에 담는 growth 묶음 (README 「growth」) @param {number} hearts @param {number} affection */
export function growthOf(hearts, affection) {
  return { hearts, heartsMax: HEARTS_MAX, affection, ...levelOf(affection) };
}

/** 완성(175)을 넘친 양은 버린다 @param {number} affection @param {number} n */
export function addAffection(affection, n) {
  return Math.min(MAX_AFFECTION, affection + n);
}
