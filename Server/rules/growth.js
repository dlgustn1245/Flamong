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
