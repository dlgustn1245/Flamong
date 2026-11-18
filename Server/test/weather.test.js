import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';
import { WEATHER_MIN, weatherOf } from '../rules/weather.js';

const DAY_MS = 86_400_000;

test('weatherOf: 5명 미만이면 인원만 · 5명부터 가장 많은 감정을 1 로 본 막대', () => {
  assert.equal(WEATHER_MIN, 5);
  assert.deepEqual(weatherOf([]), { total: 0, bars: [] });
  assert.deepEqual(weatherOf([{ emotion: 'tired', n: 3 }, { emotion: 'calm', n: 1 }]), { total: 4, bars: [] });
  // 순서는 EMOTIONS: tired · stress · achievement · focus · calm
  assert.deepEqual(
    weatherOf([{ emotion: 'calm', n: 4 }, { emotion: 'stress', n: 1 }]),
    { total: 5, bars: [0, 0.25, 0, 0, 1] },
  );
  assert.deepEqual(
    weatherOf([{ emotion: 'tired', n: 6 }, { emotion: 'stress', n: 3 }, { emotion: 'achievement', n: 4 }, { emotion: 'focus', n: 2 }, { emotion: 'calm', n: 8 }]),
    { total: 23, bars: [0.75, 0.38, 0.5, 0.25, 1] },
  );
});

test('POST /emotions 의 weather: 오늘이 아니라 어제 것 · 4명이면 막대 없음 · 5명이면 막대', async () => {
  const db = openDb(':memory:');
  let t = new Date('2026-09-23T00:00:00Z');
  const app = buildApp({ db, now: () => t });
  const players = [];
  for (let i = 0; i < 6; i++) {
    const res = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커', name: '이현수', nickname: `p${i}`, password: '1234' } });
    const h = { 'x-player-id': res.json().playerId };
    await app.inject({ method: 'POST', url: '/characters', headers: h, payload: { eggId: 0 } });
    players.push(h);
  }
  const pick = async (i, emotion) => (await app.inject({ method: 'POST', url: '/emotions', headers: players[i], payload: { emotion } })).json();

  // 첫날: 4명. 같은 날 고른 사람에게는 오늘 것이 보이지 않는다
  await pick(0, 'tired');
  await pick(1, 'tired');
  await pick(2, 'calm');
  assert.deepEqual((await pick(3, 'focus')).weather, { total: 0, bars: [] });

  // 둘째 날: 어제 4명이라 인원만. 이날은 5명이 고른다
  t = new Date(t.getTime() + DAY_MS);
  assert.deepEqual((await pick(4, 'calm')).weather, { total: 4, bars: [] });
  await pick(0, 'calm');
  await pick(1, 'calm');
  await pick(2, 'calm');
  await pick(3, 'stress');

  // 셋째 날: 어제 5명(평온 4 · 스트레스 1)
  t = new Date(t.getTime() + DAY_MS);
  assert.deepEqual((await pick(5, 'achievement')).weather, { total: 5, bars: [0, 0.25, 0, 0, 1] });
});
