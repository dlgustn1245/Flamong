import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';
import { hashPassword, verifyPassword } from '../rules/password.js';

const fixed = () => new Date('2026-09-23T00:00:00Z');
const body = { team: '플링커', name: '이현수', nickname: 'max', password: '1234' };

test('가입 → 같은 닉네임 재가입은 409 → check 는 available false', async () => {
  const app = buildApp({ db: openDb(':memory:'), now: fixed });

  const first = await app.inject({ method: 'POST', url: '/players', payload: body });
  assert.equal(first.statusCode, 200);
  assert.match(first.json().playerId, /^[0-9a-f-]{36}$/);

  const dup = await app.inject({ method: 'POST', url: '/players', payload: body });
  assert.equal(dup.statusCode, 409);
  assert.deepEqual(dup.json(), { error: 'nickname_taken' });

  const check = await app.inject({ method: 'GET', url: '/players/check?nickname=max' });
  assert.equal(check.json().available, false);
});

test('정보 수정: 보낸 값만 바뀌고 event 를 남김', async () => {
  const db = openDb(':memory:');
  const app = buildApp({ db, now: fixed });
  const { playerId } = (await app.inject({ method: 'POST', url: '/players', payload: body })).json();
  const res = await app.inject({ method: 'PATCH', url: '/players/me', headers: { 'x-player-id': playerId }, payload: { team: '서북' } });
  assert.equal(res.statusCode, 200);
  assert.deepEqual({ ...db.prepare('SELECT team, name FROM player').get() }, { team: '서북', name: body.name });   // 행은 프로토타입 없는 객체라 펼쳐서 비교
  assert.equal(db.prepare("SELECT payload FROM event WHERE type = 'profile'").get().payload, '{"team":"서북"}');
});

test('삭제 → 수정은 401 → 같은 닉네임 재가입 200 · event 는 남음', async () => {
  const db = openDb(':memory:');
  const app = buildApp({ db, now: fixed });
  const { playerId } = (await app.inject({ method: 'POST', url: '/players', payload: body })).json();
  const headers = { 'x-player-id': playerId };

  assert.equal((await app.inject({ method: 'DELETE', url: '/players/me', headers })).statusCode, 200);
  assert.equal((await app.inject({ method: 'DELETE', url: '/players/me', headers })).statusCode, 401);
  assert.equal((await app.inject({ method: 'PATCH', url: '/players/me', headers, payload: { team: 'x' } })).statusCode, 401);
  assert.equal((await app.inject({ method: 'POST', url: '/players', payload: body })).statusCode, 200);
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'delete'").get().n, 1);
});

test('아틀라스 밖 글자 닉네임은 400', async () => {
  const app = buildApp({ db: openDb(':memory:'), now: fixed });
  const res = await app.inject({ method: 'POST', url: '/players', payload: { ...body, nickname: '뷁' } });
  assert.equal(res.statusCode, 400);
  assert.equal(res.json().error, 'nickname_charset');
});

test('스키마 검증 실패와 없는 경로도 같은 형식', async () => {
  const app = buildApp({ db: openDb(':memory:'), now: fixed });
  const bad = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커' } });
  assert.equal(bad.statusCode, 400);
  assert.equal(bad.json().error, 'FST_ERR_VALIDATION');

  const missing = await app.inject({ method: 'GET', url: '/nope' });
  assert.deepEqual(missing.json(), { error: 'not_found' });
});

test('비밀번호 해시: 같은 값도 매번 다름 · 맞으면 통과 · 틀리면 거절', () => {
  const a = hashPassword('1234');
  assert.notEqual(a, hashPassword('1234'));
  assert.equal(verifyPassword('1234', a), true);
  assert.equal(verifyPassword('1235', a), false);
});

test('가입 비밀번호는 숫자 4~6자리만 · DB 에는 원문이 없음', async () => {
  const db = openDb(':memory:');
  const app = buildApp({ db, now: fixed });
  for (const password of [undefined, '123', '1234567', 'abcd', '12a4']) {
    const res = await app.inject({ method: 'POST', url: '/players', payload: { ...body, password } });
    assert.equal(res.statusCode, 400, `password ${password}`);
  }
  assert.equal((await app.inject({ method: 'POST', url: '/players', payload: { ...body, password: '123456' } })).statusCode, 200);
  const stored = db.prepare('SELECT password_hash FROM player').get().password_hash;
  assert.notEqual(stored, '123456');
  assert.equal(verifyPassword('123456', stored), true);
});

test('로그인: 닉네임 + 비밀번호로 같은 playerId · 틀린 비밀번호와 없는 닉네임은 같은 401', async () => {
  const app = buildApp({ db: openDb(':memory:'), now: fixed });
  const { playerId } = (await app.inject({ method: 'POST', url: '/players', payload: body })).json();

  const ok = await app.inject({ method: 'POST', url: '/players/login', payload: { nickname: 'max', password: '1234' } });
  assert.deepEqual(ok.json(), { playerId });

  const wrong = await app.inject({ method: 'POST', url: '/players/login', payload: { nickname: 'max', password: '9999' } });
  const nobody = await app.inject({ method: 'POST', url: '/players/login', payload: { nickname: 'nobody', password: '1234' } });
  assert.deepEqual(wrong.json(), { error: 'login_failed' });
  assert.deepEqual(nobody.json(), wrong.json());
});

test('로그인: 5번 틀리면 10분 잠금 → 맞는 비밀번호도 429 → 10분 뒤 통과, 횟수 초기화', async () => {
  let t = new Date('2026-09-23T00:00:00Z');
  const app = buildApp({ db: openDb(':memory:'), now: () => t });
  await app.inject({ method: 'POST', url: '/players', payload: body });
  const login = (password) => app.inject({ method: 'POST', url: '/players/login', payload: { nickname: 'max', password } });

  for (let i = 1; i <= 4; i++) assert.equal((await login('0000')).statusCode, 401, `${i}번째`);
  assert.deepEqual((await login('0000')).json(), { error: 'locked' });   // 5번째에 잠김
  assert.equal((await login('1234')).statusCode, 429);

  t = new Date(t.getTime() + 9 * 60_000);
  assert.equal((await login('1234')).statusCode, 429);   // 9분 뒤에도 잠김
  t = new Date(t.getTime() + 60_000);
  assert.equal((await login('1234')).statusCode, 200);   // 10분 뒤 풀림

  for (let i = 1; i <= 4; i++) assert.equal((await login('0000')).statusCode, 401);   // 성공하면 횟수가 0 부터
  assert.equal((await login('1234')).statusCode, 200);
});
