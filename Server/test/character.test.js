import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';
import { kstDate, levelOf, rechargeHearts } from '../rules/growth.js';
import { pickSpecies } from '../rules/species.js';

const DAY_MS = 86_400_000;

// 테스트 안에서 하루씩 넘길 수 있는 시계. 시작은 2026-09-23 09:00 KST
function setup() {
  const db = openDb(':memory:');
  let t = new Date('2026-09-23T00:00:00Z');
  const app = buildApp({ db, now: () => t });
  const nextDay = (n = 1) => { t = new Date(t.getTime() + n * DAY_MS); };
  return { db, app, nextDay };
}

async function signup(app) {
  const res = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커', name: '이현수', nickname: 'max', password: '1234' } });
  return { 'x-player-id': res.json().playerId };
}

// 본문 없는 POST 도 클라이언트처럼 {} 를 보낸다 (JSON 빈 본문은 Fastify 가 400)
async function call(app, method, url, headers, payload) {
  const res = await app.inject({ method, url, headers, payload });
  return { code: res.statusCode, body: res.json() };
}

const growth = (hearts, affection, level, levelProgress, levelNeed, completed = false) =>
  ({ hearts, heartsMax: 15, affection, level, levelProgress, levelNeed, completed });

test('levelOf: 레벨 경계값과 완성', () => {
  const cases = [
    [0, 1, 0, 5], [4, 1, 4, 5], [5, 2, 0, 25], [29, 2, 24, 25], [30, 3, 0, 35],
    [65, 4, 0, 50], [114, 4, 49, 50], [115, 5, 0, 60], [174, 5, 59, 60],
  ];
  for (const [a, level, levelProgress, levelNeed] of cases) {
    assert.deepEqual(levelOf(a), { level, levelProgress, levelNeed, completed: false }, `affection ${a}`);
  }
  assert.deepEqual(levelOf(175), { level: 5, levelProgress: 60, levelNeed: 60, completed: true });
});

test('하트 충전: 하루 5개 · 최대 15개 · 날짜는 KST 0시에 바뀜', () => {
  assert.equal(kstDate(new Date('2026-09-23T14:59:59Z')), '2026-09-23');
  assert.equal(kstDate(new Date('2026-09-23T15:00:00Z')), '2026-09-24');
  assert.equal(rechargeHearts(2, '2026-09-23', '2026-09-23'), 2);
  assert.equal(rechargeHearts(0, '2026-09-23', '2026-09-24'), 5);
  assert.equal(rechargeHearts(12, '2026-09-23', '2026-09-24'), 15);
  assert.equal(rechargeHearts(0, '2026-09-23', '2026-10-03'), 15);
  assert.equal(rechargeHearts(3, '2026-09-24', '2026-09-23'), 3);   // 시계가 거꾸로 가도 줄지 않는다
});

test('pickSpecies: 고른 알 계열 안에서만 뽑는다', () => {
  assert.equal(pickSpecies(2, () => 0), 6);
  assert.equal(pickSpecies(2, () => 0.9999), 8);
});

test('X-Player-Id 가 없거나 모르는 id 면 401 · 404 는 없는 주소에만', async () => {
  const { app } = setup();
  assert.deepEqual(await call(app, 'GET', '/me'), { code: 401, body: { error: 'no_player' } });
  assert.deepEqual(await call(app, 'GET', '/me', { 'x-player-id': 'nope' }), { code: 401, body: { error: 'unknown_player' } });
  assert.equal((await call(app, 'POST', '/tap', { 'x-player-id': 'nope' }, {})).code, 401);
  assert.equal((await call(app, 'POST', '/characters', { 'x-player-id': 'nope' }, { eggId: 0 })).code, 401);
  assert.equal((await call(app, 'POST', '/characters/reset', { 'x-player-id': 'nope' }, {})).code, 401);
  assert.equal((await call(app, 'GET', '/nope')).code, 404);
});

test('가입 → 알 고르기 → 첫날 하트 5개로 Lv2 → 하트 소진 → 다음 날 충전', async () => {
  const { app, db, nextDay } = setup();
  const h = await signup(app);

  const me = await call(app, 'GET', '/me', h);
  assert.deepEqual([me.body.team, me.body.name, me.body.nickname], ['플링커', '이현수', 'max']);
  assert.equal(me.body.speciesId, -1);
  assert.equal(me.body.emotionToday, false);
  assert.deepEqual(me.body.growth, growth(5, 0, 1, 0, 5));
  assert.equal((await call(app, 'POST', '/tap', h, {})).body.error, 'no_character');

  const hatch = await call(app, 'POST', '/characters', h, { eggId: 2 });
  assert.ok(hatch.body.speciesId >= 6 && hatch.body.speciesId <= 8);
  assert.equal((await call(app, 'GET', '/me', h)).body.speciesId, hatch.body.speciesId);
  // 다시 보내면(재시도) 새로 뽑지 않고 같은 캐릭터를 돌려준다
  assert.deepEqual(await call(app, 'POST', '/characters', h, { eggId: 3 }), { code: 200, body: { speciesId: hatch.body.speciesId } });
  assert.equal(db.prepare('SELECT count(*) n FROM character').get().n, 1);
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'hatch'").get().n, 1);
  assert.equal((await call(app, 'POST', '/characters', h, { eggId: 8 })).code, 400);

  for (let i = 0; i < 4; i++) assert.equal((await call(app, 'POST', '/tap', h, {})).body.levelUp, false);
  const fifth = await call(app, 'POST', '/tap', h, {});
  assert.equal(fifth.body.levelUp, true);
  assert.deepEqual(fifth.body.growth, growth(0, 5, 2, 0, 25));
  assert.equal((await call(app, 'POST', '/tap', h, {})).body.error, 'no_hearts');
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'tap'").get().n, 5);

  nextDay();
  assert.equal((await call(app, 'GET', '/me', h)).body.growth.hearts, 5);
  nextDay(3);   // 안 쓰고 사흘 더 → 20 이지만 상한 15
  assert.equal((await call(app, 'GET', '/me', h)).body.growth.hearts, 15);
  assert.equal((await call(app, 'POST', '/tap', h, {})).body.growth.hearts, 14);
});

test('오늘의 감정: +2 · 하루 1회 · 두 번째는 애정도도 오르지 않음 · 다음 날 다시', async () => {
  const { app, nextDay } = setup();
  const h = await signup(app);
  await call(app, 'POST', '/characters', h, { eggId: 0 });

  assert.equal((await call(app, 'POST', '/emotions', h, { emotion: 'angry' })).code, 400);
  const first = await call(app, 'POST', '/emotions', h, { emotion: 'calm' });
  assert.deepEqual(first.body, { levelUp: false, growth: growth(5, 2, 1, 2, 5) });
  assert.equal((await call(app, 'GET', '/me', h)).body.emotionToday, true);

  assert.equal((await call(app, 'POST', '/emotions', h, { emotion: 'focus' })).body.error, 'emotion_done');
  assert.equal((await call(app, 'GET', '/me', h)).body.growth.affection, 2);

  nextDay();
  assert.equal((await call(app, 'GET', '/me', h)).body.emotionToday, false);
  const second = await call(app, 'POST', '/emotions', h, { emotion: 'focus' });
  assert.equal(second.body.levelUp, false);
  assert.equal(second.body.growth.affection, 4);
  const crossing = await call(app, 'POST', '/tap', h, {});   // 4 → 5 레벨업은 탭으로도
  assert.equal(crossing.body.levelUp, true);
});

test('새로 키우기: 지금 캐릭터는 보관 · 하트는 그대로 · 재시도해도 ok · 알을 다시 고르면 새 캐릭터', async () => {
  const { app, db } = setup();
  const h = await signup(app);
  await call(app, 'POST', '/characters', h, { eggId: 0 });
  for (let i = 0; i < 3; i++) await call(app, 'POST', '/tap', h, {});

  assert.deepEqual(await call(app, 'POST', '/characters/reset', h, {}), { code: 200, body: { ok: true } });
  const me = await call(app, 'GET', '/me', h);
  assert.equal(me.body.speciesId, -1);   // 클라이언트는 EggSelect 로
  assert.deepEqual(me.body.growth, growth(2, 0, 1, 0, 5));   // 하트 5 - 3 = 2 유지, 애정도 · 레벨은 처음부터
  assert.equal((await call(app, 'POST', '/tap', h, {})).body.error, 'no_character');

  // 응답을 못 받고 다시 보내도 ok, 기록은 한 번
  assert.deepEqual(await call(app, 'POST', '/characters/reset', h, {}), { code: 200, body: { ok: true } });
  const events = db.prepare("SELECT payload FROM event WHERE type = 'reset'").all();
  assert.equal(events.length, 1);
  assert.equal(JSON.parse(events[0].payload).completed, false);

  const hatch = await call(app, 'POST', '/characters', h, { eggId: 5 });
  assert.ok(hatch.body.speciesId >= 15 && hatch.body.speciesId <= 17);
  assert.equal((await call(app, 'GET', '/me', h)).body.speciesId, hatch.body.speciesId);
  // 이전 캐릭터는 지우지 않고 애정도와 함께 보관
  const rows = db.prepare('SELECT affection, ended_at FROM character ORDER BY id').all();
  assert.equal(rows.length, 2);
  assert.equal(rows[0].affection, 3);
  assert.ok(rows[0].ended_at);
  assert.equal(rows[1].ended_at, null);
});

test('도감: 지금 캐릭터는 도달 레벨까지 · 완성은 남음 · 완성 전 리셋은 안 남음 · 401', async () => {
  const { app, db } = setup();
  const h = await signup(app);
  const levels = (entries) => { const a = new Array(24).fill(0); for (const [s, lv] of entries) a[s] = lv; return a; };
  assert.deepEqual(await call(app, 'GET', '/collection', h), { code: 200, body: { levels: levels([]), completed: [], speciesId: -1 } });

  await call(app, 'POST', '/characters', h, { eggId: 3 });
  const first = (await call(app, 'GET', '/me', h)).body.speciesId;
  assert.deepEqual((await call(app, 'GET', '/collection', h)).body.levels, levels([[first, 1]]));   // 막 깨어나도 Lv1 은 보인다
  for (let i = 0; i < 5; i++) await call(app, 'POST', '/tap', h, {});
  assert.deepEqual((await call(app, 'GET', '/collection', h)).body.levels, levels([[first, 2]]));

  db.prepare('UPDATE character SET affection = 175').run();
  const done = { levels: levels([[first, 5]]), completed: [first], speciesId: first };
  assert.deepEqual((await call(app, 'GET', '/collection', h)).body, done);

  // 완성 뒤 새로 키우기 → 그대로 남는다. 새 캐릭터는 키우는 동안 보이다가, 완성 전에 리셋하면 사라진다
  await call(app, 'POST', '/characters/reset', h, {});
  assert.deepEqual((await call(app, 'GET', '/collection', h)).body, { ...done, speciesId: -1 });
  await call(app, 'POST', '/characters', h, { eggId: 0 });
  const second = (await call(app, 'GET', '/me', h)).body.speciesId;
  assert.deepEqual((await call(app, 'GET', '/collection', h)).body.levels, levels([[first, 5], [second, 1]]));
  await call(app, 'POST', '/characters/reset', h, {});
  assert.deepEqual((await call(app, 'GET', '/collection', h)).body, { ...done, speciesId: -1 });

  assert.equal((await call(app, 'GET', '/collection', { 'x-player-id': 'nope' })).code, 401);
});

test('완성(175): 레벨업 없이 completed → 이후 탭은 409 · 감정은 기록만', async () => {
  const { app, db } = setup();
  const h = await signup(app);
  await call(app, 'POST', '/characters', h, { eggId: 0 });
  db.prepare('UPDATE character SET affection = 174').run();

  const last = await call(app, 'POST', '/tap', h, {});
  assert.deepEqual(last.body, { levelUp: false, growth: growth(4, 175, 5, 60, 60, true) });
  assert.equal((await call(app, 'POST', '/tap', h, {})).body.error, 'completed');

  const emotion = await call(app, 'POST', '/emotions', h, { emotion: 'calm' });
  assert.equal(emotion.code, 200);
  assert.equal(emotion.body.growth.affection, 175);
  assert.equal(db.prepare('SELECT count(*) n FROM emotion_log').get().n, 1);

  // 캐릭터 · 감정 기록이 있어도 탈퇴는 된다 (character 가 player 를 참조)
  assert.equal((await call(app, 'DELETE', '/players/me', h)).code, 200);
  assert.equal(db.prepare('SELECT count(*) n FROM emotion_log').get().n, 0);
});
