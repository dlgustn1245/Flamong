// 미니게임 수치의 단일 출처. README 「미니게임 · 랭킹」과 같아야 한다.
// 한 판의 수치는 시작 응답(rules)으로 클라이언트에 내려 준다. 서버의 시간 검사 · 상한과 클라이언트 판이 따로 바뀌지 않는다

// 마음 정리. comboAt: 맞히기 전 콤보가 이 값 이상이면 1점씩 더한다(0~9 1점 · 10~29 2점 · 30 이상 3점)
export const TIDY = { durationSec: 30, countdownSec: 3, minTapMs: 100, lockMs: 600, changeEvery: 10, maxRun: 4, comboAt: [10, 30] };
// 짝꿍 찾기. 짝 하나 pairScore, 안 틀리고 이어 맞추면 streakStep 씩 늘어 streakMax 까지(10 · 15 · 20 · 25 · 30). 틀리면 missMs 동안 보여 주고 뒤집는다.
// 새 판마다 previewMs 동안 모두 보여 준다(판 시계는 흐른다). similarAt: 판 번호(0부터)가 이 값 이상이면 같은 알의 2종 묶음이 하나씩 는다
export const PAIRS = { durationSec: 30, countdownSec: 3, minTapMs: 150, cards: 12, previewMs: 1000, missMs: 600, pairScore: 10, streakStep: 5, streakMax: 30, clearBonus: 20, similarAt: [2, 4] };
export const GAMES = { tidy: TIDY, pairs: PAIRS };

// 이달의 게임 [시작 달, 게임]. 표에 없는 달은 바로 앞 줄의 게임을 잇고, 첫 줄보다 앞선 달(출시 전)은 게임이 없다.
// 새 게임을 그달의 게임으로 올릴 때 한 줄을 더한다
export const GAME_OF_MONTH = [['2026-11', 'tidy'], ['2026-12', 'pairs']];

/** @param {string} month YYYY-MM @param {string[][]} table @returns {string | null} */
export function gameOfMonth(month, table = GAME_OF_MONTH) {
  let game = null;
  for (const [from, g] of table) {
    if (from <= month) game = g;
  }
  return game;
}

export const MAX_PLAY_MS = 10 * 60_000;   // 시작에서 이보다 늦게 온 점수는 받지 않는다
export const RANKING_TOP = 20;

/** 연속 n 개를 모두 맞힌 점수 @param {number} n @param {number[]} comboAt */
export function streakScore(n, comboAt) {
  let s = 0;
  for (let k = 0; k < n; k++) s += 1 + comboAt.filter((c) => k >= c).length;
  return s;
}

/**
 * 규칙상 최고 점수. 입력 간격 하한으로 한 판에 누를 수 있는 수를 모두 맞혔을 때. +1 은 프레임 오차 여유
 * ponytail: 상한 안의 조작은 막지 못한다. 운영자가 score 행을 지운다(README 「운영자 복구」)
 * @param {typeof TIDY} g
 */
export const maxScore = (g) => streakScore(Math.floor(g.durationSec * 1000 / g.minTapMs) + 1, g.comboAt);

/**
 * 짝꿍 찾기의 규칙상 최고 점수. 한 판을 미리 보기 + 카드 수 × 입력 간격 하한에 끝내고 한 번도 틀리지 않았을 때. +1 판은 여유
 * @param {typeof PAIRS} g
 */
export function pairsMaxScore(g) {
  const boards = Math.floor(g.durationSec * 1000 / (g.previewMs + g.cards * g.minTapMs)) + 1;
  let s = boards * g.clearBonus;
  for (let k = 0; k < boards * g.cards / 2; k++) s += Math.min(g.streakMax, g.pairScore + k * g.streakStep);
  return s;
}

export const MAX_SCORE = { tidy: maxScore(TIDY), pairs: pairsMaxScore(PAIRS) };

/**
 * 시작에서 점수까지 최소 시간. 클라이언트는 시작 응답을 받은 뒤 카운트다운을 시작하므로 서버가 잰 시간이 늘 더 길다.
 * 1초는 서버 시계가 보정으로 뒤로 갈 때의 여유
 * @param {typeof TIDY} g
 */
export const minPlayMs = (g) => (g.countdownSec + g.durationSec - 1) * 1000;
