import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';
import { isLimited } from '../rules/rateLimit.js';

test('isLimited: 창 안에서 max 까지 통과, 넘으면 막고, 창이 지나면 다시 센다', () => {
  const hits = new Map();
  for (let i = 0; i < 3; i++) assert.equal(isLimited(hits, 'k', 3, 1000, 0), false);
  assert.equal(isLimited(hits, 'k', 3, 1000, 999), true);
  assert.equal(isLimited(hits, 'k', 3, 1000, 1000), false);
});

test('가입은 IP당 10분에 30번. 31번째는 429, 10분 뒤 다시 된다. 다른 라우트는 따로 센다', async () => {
  let t = new Date('2026-11-03T00:00:00Z');
  const app = buildApp({ db: openDb(':memory:'), now: () => t });
  // 본문 검사 전에 세므로 빈 본문(400)으로 비밀번호 해시 없이 빠르게 센다
  for (let i = 0; i < 30; i++) {
    assert.equal((await app.inject({ method: 'POST', url: '/players', payload: {} })).statusCode, 400);
  }
  const over = await app.inject({ method: 'POST', url: '/players', payload: {} });
  assert.equal(over.statusCode, 429);
  assert.deepEqual(over.json(), { error: 'rate_limited' });
  assert.equal((await app.inject({ method: 'GET', url: '/players/check?nickname=x' })).statusCode, 200);

  t = new Date(t.getTime() + 10 * 60_000);
  assert.equal((await app.inject({ method: 'POST', url: '/players', payload: {} })).statusCode, 400);
});
