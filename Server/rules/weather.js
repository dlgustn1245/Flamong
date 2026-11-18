import { EMOTIONS } from './growth.js';

// 어제의 마음 날씨: 어제 모두가 고른 감정의 분포. 순수 함수 (DB · HTTP 모름)
// 오늘 것은 하루 중에 계속 바뀌고 아침에는 누가 골랐는지 짐작되므로 어제 것만 쓴다.

// 이보다 적게 모인 날은 막대를 보여 주지 않는다. 개인이 짐작되지 않게 (2.0 대시보드의 「5명 이상」과 같은 기준)
export const WEATHER_MIN = 5;

/**
 * @param {{ emotion: string, n: number }[]} rows 어제 감정별 인원
 * @returns {{ total: number, bars: number[] }} bars 는 EMOTIONS 순서로 가장 많은 감정을 1 로 본 길이(0~1).
 *   인원 수는 total 만 보낸다. WEATHER_MIN 미만이면 bars 는 빈 배열
 */
export function weatherOf(rows) {
  const counts = EMOTIONS.map((e) => rows.find((r) => r.emotion === e)?.n ?? 0);
  const total = counts.reduce((a, b) => a + b, 0);
  if (total < WEATHER_MIN) return { total, bars: [] };
  const max = Math.max(...counts);
  return { total, bars: counts.map((n) => Math.round((n / max) * 100) / 100) };
}
