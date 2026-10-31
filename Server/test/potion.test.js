import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';
import { visitStatus } from '../rules/growth.js';

const DAY_MS = 86_400_000;

function setup() {
  const db = openDb(':memory:');
  let t = new Date('2026-09-23T00:00:00Z');
  const app = buildApp({ db, now: () => t });
  const nextDay = () => { t = new Date(t.getTime() + DAY_MS); };
  return { db, app, nextDay };
}

async function call(app, method, url, headers, payload) {
  const res = await app.inject({ method, url, headers, payload });
  return { code: res.statusCode, body: res.json() };
}

async function signupWithCharacter(app) {
  const res = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커', name: '이현수', nickname: 'max', password: '1234' } });
  const h = { 'x-player-id': res.json().playerId };
  await call(app, 'POST', '/characters', h, { eggId: 0 });
  return h;
}

// 오늘의 감정을 n 일 고른다 (하루에 한 번만 되므로 날을 넘기며)
async function visit(app, h, nextDay, n) {
  for (let i = 0; i < n; i++) {
    assert.equal((await call(app, 'POST', '/emotions', h, { emotion: 'calm' })).code, 200);
    nextDay();
  }
}

const hidden = (completeDone, completeClaimed = false) => [
  { id: 'first_complete', done: completeDone, claimed: completeClaimed },
  { id: 'first_game', done: false, claimed: false },
  { id: 'first_garden', done: false, claimed: false },
];

test('visitStatus: 받기 전에는 7 에서 멈추고 남은 방문은 받은 뒤 이어진다', () => {
  assert.deepEqual(visitStatus(3, 0), { progress: 3, need: 7 });
  assert.deepEqual(visitStatus(15, 0), { progress: 7, need: 7 });
  assert.deepEqual(visitStatus(15, 1), { progress: 7, need: 7 });
  assert.deepEqual(visitStatus(15, 2), { progress: 1, need: 7 });
});

test('방문 묘약: 감정 7일(연속 아님)마다 받기 · +10 · 7일 전엔 not_ready · 늦게 받아도 손해 없음', async () => {
  const { app, db, nextDay } = setup();
  const h = await signupWithCharacter(app);
  assert.deepEqual(await call(app, 'GET', '/missions', h), { code: 200, body: { canDrink: true, visit: { progress: 0, need: 7 }, hidden: hidden(false) } });

  await visit(app, h, nextDay, 6);
  nextDay();   // 하루 빠져도 끊기지 않는다
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, false);
  assert.deepEqual(await call(app, 'POST', '/potions/claim', h, { missionId: 'visit' }), { code: 409, body: { error: 'not_ready' } });

  await visit(app, h, nextDay, 1);   // 7일째 · 애정도 14
  assert.deepEqual((await call(app, 'GET', '/missions', h)).body.visit, { progress: 7, need: 7 });
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, true);
  const drink = await call(app, 'POST', '/potions/claim', h, { missionId: 'visit' });
  assert.equal(drink.code, 200);
  assert.equal(drink.body.levelUp, false);
  assert.equal(drink.body.growth.affection, 24);
  assert.deepEqual((await call(app, 'GET', '/missions', h)).body.visit, { progress: 0, need: 7 });
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, false);
  assert.equal((await call(app, 'POST', '/potions/claim', h, { missionId: 'visit' })).code, 409);   // 재시도해도 한 번만

  // 14일 동안 안 받으면 7/7 에서 멈췄다가, 두 번 받을 수 있다
  await visit(app, h, nextDay, 14);
  assert.deepEqual((await call(app, 'GET', '/missions', h)).body.visit, { progress: 7, need: 7 });
  assert.equal((await call(app, 'POST', '/potions/claim', h, { missionId: 'visit' })).code, 200);
  assert.deepEqual((await call(app, 'GET', '/missions', h)).body.visit, { progress: 7, need: 7 });
  assert.equal((await call(app, 'POST', '/potions/claim', h, { missionId: 'visit' })).code, 200);
  assert.deepEqual((await call(app, 'GET', '/missions', h)).body.visit, { progress: 0, need: 7 });

  const rows = db.prepare("SELECT mission_id FROM potion ORDER BY mission_id").all().map((r) => r.mission_id);
  assert.deepEqual(rows, ['visit:1', 'visit:2', 'visit:3']);
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'potion'").get().n, 3);
});

test('히든 미션 첫 완성: 완성한 캐릭터에겐 못 마심 → 새로 키울 때 마심 · 한 번만 · 레벨업', async () => {
  const { app, db } = setup();
  const h = await signupWithCharacter(app);
  assert.deepEqual(await call(app, 'POST', '/potions/claim', h, { missionId: 'first_complete' }), { code: 409, body: { error: 'not_ready' } });

  db.prepare('UPDATE character SET affection = 175').run();
  assert.deepEqual((await call(app, 'GET', '/missions', h)).body, { canDrink: false, visit: { progress: 0, need: 7 }, hidden: hidden(true) });
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, false);   // 마실 수 없으니 빨간 점도 없다
  assert.deepEqual(await call(app, 'POST', '/potions/claim', h, { missionId: 'first_complete' }), { code: 409, body: { error: 'completed' } });

  await call(app, 'POST', '/characters/reset', h, {});
  assert.deepEqual(await call(app, 'POST', '/potions/claim', h, { missionId: 'first_complete' }), { code: 409, body: { error: 'no_character' } });
  await call(app, 'POST', '/characters', h, { eggId: 1 });
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, true);

  const drink = await call(app, 'POST', '/potions/claim', h, { missionId: 'first_complete' });
  assert.equal(drink.code, 200);
  assert.equal(drink.body.levelUp, true);   // 0 → 10 은 Lv1 → Lv2
  assert.equal(drink.body.growth.affection, 10);
  assert.deepEqual((await call(app, 'GET', '/missions', h)).body.hidden, hidden(true, true));
  assert.deepEqual(await call(app, 'POST', '/potions/claim', h, { missionId: 'first_complete' }), { code: 409, body: { error: 'claimed' } });
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, false);
});

test('묘약: 모르는 미션 400 · 계정 401', async () => {
  const { app } = setup();
  const h = await signupWithCharacter(app);
  assert.equal((await call(app, 'POST', '/potions/claim', h, { missionId: 'nope' })).code, 400);
  assert.equal((await call(app, 'POST', '/potions/claim', h, {})).code, 400);
  assert.equal((await call(app, 'GET', '/missions', { 'x-player-id': 'nope' })).code, 401);
  assert.equal((await call(app, 'POST', '/potions/claim', { 'x-player-id': 'nope' }, { missionId: 'visit' })).code, 401);
});
