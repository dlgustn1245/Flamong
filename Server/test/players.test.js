import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';

const fixed = () => new Date('2026-09-23T00:00:00Z');
const body = { team: '플링커', name: '이현수', nickname: 'max' };

test('가입 → 같은 닉네임 재가입은 409 → check 는 available false', async () => {
  const app = buildApp({ db: openDb(':memory:'), now: fixed });

  const first = await app.inject({ method: 'POST', url: '/players', payload: body });
  assert.equal(first.statusCode, 200);
  assert.equal(first.json().statusCode, 200);
  assert.match(first.json().body.playerId, /^[0-9a-f-]{36}$/);

  const dup = await app.inject({ method: 'POST', url: '/players', payload: body });
  assert.equal(dup.statusCode, 409);
  assert.deepEqual(dup.json(), { statusCode: 409, body: { error: 'nickname_taken' } });

  const check = await app.inject({ method: 'GET', url: '/players/check?nickname=max' });
  assert.equal(check.json().body.available, false);
});

test('삭제 → 수정은 404 → 같은 닉네임 재가입 200 · event 는 남음', async () => {
  const db = openDb(':memory:');
  const app = buildApp({ db, now: fixed });
  const { playerId } = (await app.inject({ method: 'POST', url: '/players', payload: body })).json().body;
  const headers = { 'x-player-id': playerId };

  assert.equal((await app.inject({ method: 'DELETE', url: '/players/me', headers })).statusCode, 200);
  assert.equal((await app.inject({ method: 'DELETE', url: '/players/me', headers })).statusCode, 404);
  assert.equal((await app.inject({ method: 'PATCH', url: '/players/me', headers, payload: { team: 'x' } })).statusCode, 404);
  assert.equal((await app.inject({ method: 'POST', url: '/players', payload: body })).statusCode, 200);
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'delete'").get().n, 1);
});

test('아틀라스 밖 글자 닉네임은 400', async () => {
  const app = buildApp({ db: openDb(':memory:'), now: fixed });
  const res = await app.inject({ method: 'POST', url: '/players', payload: { ...body, nickname: '뷁' } });
  assert.equal(res.statusCode, 400);
  assert.equal(res.json().body.error, 'nickname_charset');
});

test('스키마 검증 실패와 없는 경로도 같은 형식', async () => {
  const app = buildApp({ db: openDb(':memory:'), now: fixed });
  const bad = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커' } });
  assert.equal(bad.statusCode, 400);
  assert.equal(bad.json().statusCode, 400);
  assert.equal(bad.json().body.error, 'FST_ERR_VALIDATION');

  const missing = await app.inject({ method: 'GET', url: '/nope' });
  assert.deepEqual(missing.json(), { statusCode: 404, body: { error: 'not_found' } });
});
