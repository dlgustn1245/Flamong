import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';
import { addMonth, calendarOf } from '../rules/calendar.js';

const DAY_MS = 86_400_000;

test('addMonth: 해를 넘어간다', () => {
  assert.equal(addMonth('2026-11', 1), '2026-12');
  assert.equal(addMonth('2026-12', 1), '2027-01');
  assert.equal(addMonth('2027-01', -1), '2026-12');
});

test('calendarOf: 날수 · 1일의 요일 · 오늘 · 넘길 수 있는 달', () => {
  const nov = calendarOf('2026-11', [{ date: '2026-11-02', emotion: 'calm' }, { date: '2026-11-30', emotion: 'tired' }], '2026-10', '2026-11-18');
  assert.equal(nov.emotions.length, 30);
  assert.equal(nov.firstWeekday, 0);   // 2026-11-01 은 일요일
  assert.deepEqual([nov.emotions[0], nov.emotions[1], nov.emotions[29]], ['', 'calm', 'tired']);
  assert.deepEqual([nov.today, nov.prev, nov.next], [18, '2026-10', '']);   // 이번 달 뒤로는 못 넘긴다

  const oct = calendarOf('2026-10', [], '2026-10', '2026-11-18');
  assert.deepEqual([oct.emotions.length, oct.firstWeekday, oct.today, oct.prev, oct.next], [31, 4, 0, '', '2026-11']);   // 가입한 달 앞으로는 못 넘긴다

  assert.equal(calendarOf('2028-02', [], '2026-10', '2028-02-10').emotions.length, 29);   // 윤년
});

test('GET /emotions: 내 기록만 · 기본은 이번 달 · 가입한 달부터 이번 달까지', async () => {
  const db = openDb(':memory:');
  let t = new Date('2026-09-23T00:00:00Z');
  const app = buildApp({ db, now: () => t });
  const signup = async (nickname) => {
    const res = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커', name: '이현수', nickname, password: '1234' } });
    const h = { 'x-player-id': res.json().playerId };
    await app.inject({ method: 'POST', url: '/characters', headers: h, payload: { eggId: 0 } });
    return h;
  };
  const get = async (h, query = '') => {
    const res = await app.inject({ method: 'GET', url: `/emotions${query}`, headers: h });
    return { code: res.statusCode, body: res.json() };
  };
  const me = await signup('me');
  const other = await signup('other');
  assert.equal((await get({})).code, 401);

  await app.inject({ method: 'POST', url: '/emotions', headers: me, payload: { emotion: 'calm' } });
  await app.inject({ method: 'POST', url: '/emotions', headers: other, payload: { emotion: 'stress' } });
  t = new Date(t.getTime() + DAY_MS);
  await app.inject({ method: 'POST', url: '/emotions', headers: other, payload: { emotion: 'tired' } });   // 24일은 다른 사람만

  const sep = (await get(me)).body;
  assert.deepEqual([sep.month, sep.firstWeekday, sep.today, sep.prev, sep.next], ['2026-09', 2, 24, '', '']);   // 2026-09-01 은 화요일
  assert.equal(sep.emotions.length, 30);
  assert.deepEqual([sep.emotions[22], sep.emotions[23]], ['calm', '']);

  // 달이 바뀌면 기본이 새 달이고, 지난달로 넘길 수 있다
  t = new Date('2026-10-03T00:00:00Z');
  await app.inject({ method: 'POST', url: '/emotions', headers: me, payload: { emotion: 'focus' } });
  const oct = (await get(me)).body;
  assert.deepEqual([oct.month, oct.today, oct.prev, oct.next, oct.emotions[2]], ['2026-10', 3, '2026-09', '', 'focus']);
  const back = (await get(me, '?month=2026-09')).body;
  assert.deepEqual([back.month, back.today, back.prev, back.next, back.emotions[22]], ['2026-09', 0, '', '2026-10', 'calm']);

  assert.deepEqual(await get(me, '?month=2026-08'), { code: 400, body: { error: 'month_range' } });   // 가입 전
  assert.deepEqual(await get(me, '?month=2026-11'), { code: 400, body: { error: 'month_range' } });   // 아직 오지 않은 달
  assert.equal((await get(me, '?month=2026-13')).code, 400);
});
