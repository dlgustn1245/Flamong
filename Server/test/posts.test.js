import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';

const DAY_MS = 86_400_000;

function setup() {
  const db = openDb(':memory:');
  let t = new Date('2026-09-23T00:00:00Z');   // KST 09-23
  const app = buildApp({ db, now: () => t });
  const nextDay = (n = 1) => { t = new Date(t.getTime() + n * DAY_MS); };
  return { db, app, nextDay };
}

async function call(app, method, url, headers, payload) {
  const res = await app.inject({ method, url, headers, payload });
  return { code: res.statusCode, body: res.json() };
}

async function signup(app, nickname) {
  const res = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커', name: '이현수', nickname, password: '1234' } });
  const h = { 'x-player-id': res.json().playerId };
  await call(app, 'POST', '/characters', h, { eggId: 0 });
  return h;
}

test('꽃밭: 하루 1회 · 익명 · 앞뒤 공백 지움 · 최신순 · 첫 글이면 히든 미션 달성', async () => {
  const { app, db, nextDay } = setup();
  const h = await signup(app, 'max');
  assert.deepEqual(await call(app, 'GET', '/posts?kind=garden', h), { code: 200, body: { canWrite: true, posts: [] } });

  assert.deepEqual(await call(app, 'POST', '/posts', h, { kind: 'garden', body: '  됬어요 ㅋㅋ  ' }), { code: 200, body: { ok: true } });
  assert.deepEqual((await call(app, 'GET', '/posts?kind=garden', h)).body, {
    canWrite: false,
    posts: [{ body: '됬어요 ㅋㅋ', date: '2026-09-23', nickname: '' }],
  });
  assert.deepEqual(await call(app, 'POST', '/posts', h, { kind: 'garden', body: '또' }), { code: 409, body: { error: 'posted' } });
  assert.equal((await call(app, 'GET', '/missions', h)).body.hidden.find((m) => m.id === 'first_garden').done, true);
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'post'").get().n, 1);

  nextDay();
  assert.equal((await call(app, 'GET', '/posts?kind=garden', h)).body.canWrite, true);
  await call(app, 'POST', '/posts', h, { kind: 'garden', body: '이틀째' });
  assert.deepEqual((await call(app, 'GET', '/posts?kind=garden', h)).body.posts.map((p) => p.body), ['이틀째', '됬어요 ㅋㅋ']);
});

test('글: 이모지 · 공백만 · 줄바꿈 · 101자 · 모르는 kind 는 400, 모르는 계정 401', async () => {
  const { app } = setup();
  const h = await signup(app, 'max');
  assert.deepEqual(await call(app, 'POST', '/posts', h, { kind: 'garden', body: '좋아요😀' }), { code: 400, body: { error: 'body_charset' } });
  assert.deepEqual(await call(app, 'POST', '/posts', h, { kind: 'garden', body: '탷 됬' }), { code: 400, body: { error: 'body_charset' } });   // 탷은 폰트에 모양이 없다
  assert.equal((await call(app, 'POST', '/posts', h, { kind: 'garden', body: '   ' })).code, 400);
  assert.equal((await call(app, 'POST', '/posts', h, { kind: 'garden', body: '한 줄\n두 줄' })).code, 400);
  assert.equal((await call(app, 'POST', '/posts', h, { kind: 'garden', body: '가'.repeat(101) })).code, 400);
  assert.equal((await call(app, 'POST', '/posts', h, { kind: 'garden', body: '가'.repeat(100) })).code, 200);
  assert.equal((await call(app, 'POST', '/posts', h, { kind: 'nope', body: '가' })).code, 400);
  assert.equal((await call(app, 'GET', '/posts', h)).code, 400);
  assert.equal((await call(app, 'GET', '/posts?kind=garden', { 'x-player-id': 'nope' })).code, 401);
});

test('소식: 작성권 없으면 409 · 어제까지 7일 안에 온 사람 중 오늘의 담당 · 작성권 있는 사람은 담당 후보에서 빠짐', async () => {
  const { app, db, nextDay } = setup();
  const a = await signup(app, 'a');
  const b = await signup(app, 'b');
  await call(app, 'POST', '/emotions', a, { emotion: 'calm' });

  // 오늘 감정을 고른 것은 후보가 아니다 (오늘 하루 담당이 바뀌지 않게)
  assert.equal((await call(app, 'GET', '/me', a)).body.newsReady, false);
  assert.equal((await call(app, 'GET', '/posts?kind=news', a)).body.canWrite, false);
  assert.deepEqual(await call(app, 'POST', '/posts', a, { kind: 'news', body: '소식' }), { code: 409, body: { error: 'no_right' } });

  nextDay();   // a 가 유일한 후보
  assert.equal((await call(app, 'GET', '/me', a)).body.newsReady, true);
  assert.equal((await call(app, 'GET', '/me', b)).body.newsReady, false);
  assert.equal((await call(app, 'GET', '/posts?kind=news', a)).body.canWrite, true);
  assert.equal((await call(app, 'POST', '/posts', b, { kind: 'news', body: '소식' })).code, 409);
  assert.equal((await call(app, 'POST', '/posts', a, { kind: 'news', body: '오늘의 소식' })).code, 200);
  assert.equal((await call(app, 'GET', '/me', a)).body.newsReady, false);
  assert.deepEqual((await call(app, 'GET', '/posts?kind=news', b)).body, {
    canWrite: false,
    posts: [{ body: '오늘의 소식', date: '2026-09-24', nickname: 'a' }],
  });
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'news'").get().n, 1);

  db.prepare("UPDATE player SET can_post_news = 1 WHERE nickname = 'b'").run();
  assert.equal((await call(app, 'GET', '/posts?kind=news', b)).body.canWrite, true);
  assert.equal((await call(app, 'POST', '/posts', b, { kind: 'news', body: '공지' })).code, 200);

  nextDay(7);   // a 의 마지막 방문이 8일 전 → 후보 없음
  assert.equal((await call(app, 'GET', '/posts?kind=news', a)).body.canWrite, false);
});

test('목록은 최신 30개', async () => {
  const { app, db } = setup();
  const h = await signup(app, 'max');
  const insert = db.prepare("INSERT INTO post (player_id, date, kind, body) VALUES (?, ?, 'garden', ?)");
  for (let i = 1; i <= 31; i++) insert.run(h['x-player-id'], `d${i}`, String(i));
  const { posts } = (await call(app, 'GET', '/posts?kind=garden', h)).body;
  assert.equal(posts.length, 30);
  assert.equal(posts[0].body, '31');
  assert.equal(posts[29].body, '2');
});
