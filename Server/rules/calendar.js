// 마음 달력: 내가 날마다 고른 감정을 한 달 치 달력 모양으로. 순수 함수 (DB · HTTP 모름)
// 요일 · 날수 · 앞뒤 달은 여기서 정해 내려 준다. 클라이언트는 날짜 계산을 하지 않는다 (기기 시계를 믿지 않는다)

export const MONTH_PATTERN = '^\\d{4}-(0[1-9]|1[0-2])$';

/** 'YYYY-MM' 에 n 달을 더한다 @param {string} month @param {number} n */
export function addMonth(month, n) {
  const [y, m] = month.split('-').map(Number);
  return new Date(Date.UTC(y, m - 1 + n, 1)).toISOString().slice(0, 7);
}

/**
 * @param {string} month 보여 줄 달 'YYYY-MM'
 * @param {{ date: string, emotion: string }[]} rows 그 달의 내 감정 기록
 * @param {string} firstMonth 가입한 달. 그 앞으로는 넘기지 못한다
 * @param {string} today KST 'YYYY-MM-DD'. 이번 달 뒤로는 넘기지 못한다
 * @returns {{ month: string, firstWeekday: number, today: number, prev: string, next: string, emotions: string[] }}
 *   firstWeekday 는 1일의 요일(0 = 일요일), today 는 이번 달이면 오늘 날짜 · 지난 달이면 0,
 *   prev · next 는 넘길 수 있는 달(없으면 ''), emotions[일 - 1] 은 그날 고른 감정(없으면 '')
 */
export function calendarOf(month, rows, firstMonth, today) {
  const [y, m] = month.split('-').map(Number);
  const emotions = new Array(new Date(Date.UTC(y, m, 0)).getUTCDate()).fill('');
  for (const r of rows) emotions[Number(r.date.slice(8)) - 1] = r.emotion;
  const current = today.slice(0, 7);
  return {
    month,
    firstWeekday: new Date(Date.UTC(y, m - 1, 1)).getUTCDay(),
    today: month === current ? Number(today.slice(8)) : 0,
    prev: month > firstMonth ? addMonth(month, -1) : '',
    next: month < current ? addMonth(month, 1) : '',
    emotions,
  };
}
