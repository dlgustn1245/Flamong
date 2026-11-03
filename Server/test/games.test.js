import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildApp } from '../index.js';
import { openDb } from '../db.js';
import { GAMES, GAME_OF_MONTH, gameOfMonth, maxScore, minPlayMs, TIDY, streakScore } from '../rules/game.js';

const DAY_MS = 86_400_000;

function setup(iso = '2026-11-23T03:00:00Z') {
  const db = openDb(':memory:');
  let t = new Date(iso);
  const app = buildApp({ db, now: () => t });
  const advance = (ms) => { t = new Date(t.getTime() + ms); };
  return { db, app, advance };
}

async function call(app, method, url, headers, payload) {
  const res = await app.inject({ method, url, headers, payload });
  return { code: res.statusCode, body: res.json() };
}

async function signup(app) {
  const res = await app.inject({ method: 'POST', url: '/players', payload: { team: '플링커', name: '이현수', nickname: 'max', password: '1234' } });
  const h = { 'x-player-id': res.json().playerId };
  await call(app, 'POST', '/characters', h, { eggId: 0 });
  return h;
}

// 한 판: 시작 → wait 뒤 점수
async function play(app, h, advance, score, wait = 40_000) {
  assert.equal((await call(app, 'POST', '/games/start', h, { game: 'tidy' })).code, 200);
  advance(wait);
  return call(app, 'POST', '/scores', h, { score });
}

// 다른 사람 점수는 SQL 로 바로 넣는다(가입의 scrypt 를 줄인다). playerId = 닉네임
function addScore(db, nickname, best, month = '2026-11') {
  db.prepare("INSERT INTO player (id, team, name, nickname, created_at, hearts, hearts_date, password_hash) VALUES (?, 't', 'n', ?, '', 5, '2026-11-23', 'x')").run(nickname, nickname);
  db.prepare("INSERT INTO score (player_id, game, month, best_score) VALUES (?, 'tidy', ?, ?)").run(nickname, month, best);
}

test('규칙 함수: 연속 점수 · 상한 863 · 최소 32초 · 이달의 게임 표', () => {
  assert.equal(streakScore(10, TIDY.comboAt), 10);
  assert.equal(streakScore(30, TIDY.comboAt), 50);
  assert.equal(streakScore(31, TIDY.comboAt), 53);
  assert.equal(maxScore(TIDY), 863);
  assert.equal(minPlayMs(TIDY), 32_000);
  assert.equal(gameOfMonth('2026-10'), null);   // 출시 전
  assert.equal(gameOfMonth('2026-11'), 'tidy');
  assert.equal(gameOfMonth('2027-05'), 'tidy');   // 새 게임이 없으면 같은 게임을 잇는다
  const table = [['2026-11', 'tidy'], ['2027-02', 'next']];
  assert.deepEqual(['2027-01', '2027-02', '2027-09'].map((m) => gameOfMonth(m, table)), ['tidy', 'next', 'next']);
  for (const [, game] of GAME_OF_MONTH) assert.ok(GAMES[game], `${game} 는 GAMES 에 있어야 한다`);
});

test('점수 시간: 시작 없음 · 32초 전 · 10분 넘음은 409, 다시 보내면 200 · 애정도 0 · 쓰지 않음, 새 시작이 앞 시작을 덮음', async () => {
  const { app, db, advance } = setup();
  const h = await signup(app);
  assert.deepEqual(await call(app, 'POST', '/scores', h, { score: 10 }), { code: 409, body: { error: 'not_started' } });

  assert.deepEqual(await call(app, 'POST', '/games/start', h, { game: 'tidy' }), { code: 200, body: { rules: TIDY } });
  advance(31_999);
  assert.deepEqual(await call(app, 'POST', '/scores', h, { score: 10 }), { code: 409, body: { error: 'too_fast' } });
  advance(1);
  const ok = await call(app, 'POST', '/scores', h, { score: 10 });
  assert.equal(ok.code, 200);
  assert.equal(ok.body.gain, 2);
  const again = await call(app, 'POST', '/scores', h, { score: 10 });   // 응답을 못 받고 다시 보냄
  assert.deepEqual(again, { code: 200, body: { ...ok.body, gain: 0, levelUp: false } });
  assert.equal(db.prepare("SELECT count(*) n FROM event WHERE type = 'game_score'").get().n, 1);

  await call(app, 'POST', '/games/start', h, { game: 'tidy' });
  advance(20_000);
  await call(app, 'POST', '/games/start', h, { game: 'tidy' });   // 그만두고 다시 시작
  advance(20_000);
  assert.equal((await call(app, 'POST', '/scores', h, { score: 10 })).body.error, 'too_fast');   // 첫 시작에서는 40초지만 마지막 시작 기준
  advance(10 * 60_000 - 20_000 + 1);
  assert.deepEqual(await call(app, 'POST', '/scores', h, { score: 10 }), { code: 409, body: { error: 'expired' } });
});

test('형식: 863 초과 · 음수 · 소수 · 빠짐 · 모르는 게임 · 잘못된 달은 400, 모르는 계정 401', async () => {
  const { app, advance } = setup();
  const h = await signup(app);
  await call(app, 'POST', '/games/start', h, { game: 'tidy' });
  advance(40_000);
  for (const payload of [{ score: 864 }, { score: -1 }, { score: 1.5 }, {}]) {
    assert.equal((await call(app, 'POST', '/scores', h, payload)).code, 400, JSON.stringify(payload));
  }
  assert.equal((await call(app, 'POST', '/scores', h, { score: 863 })).code, 200);
  assert.equal((await call(app, 'POST', '/games/start', h, { game: 'nope' })).code, 400);
  assert.equal((await call(app, 'POST', '/games/start', h, {})).code, 400);
  for (const month of ['2026-13', '2026-1', '26-11', 'now']) {
    assert.equal((await call(app, 'GET', `/rankings?month=${month}`, h)).code, 400, month);
  }
  const nobody = { 'x-player-id': 'nope' };
  assert.equal((await call(app, 'POST', '/games/start', nobody, { game: 'tidy' })).code, 401);
  assert.equal((await call(app, 'POST', '/scores', nobody, { score: 1 })).code, 401);
  assert.equal((await call(app, 'GET', '/rankings', nobody)).code, 401);
  assert.equal((await call(app, 'GET', '/games/months', nobody)).code, 401);
});

test('하루 첫 판만 애정도 +2: KST 0시 기준 · 시작만 한 판은 세지 않음 · 레벨업 · 174 는 +1 · 완성 0 · 캐릭터가 없어도 점수는 받음', async () => {
  const { app, db, advance } = setup('2026-11-23T14:58:00Z');   // KST 23:58
  const h = await signup(app);
  db.prepare('UPDATE character SET affection = 4').run();
  const first = await play(app, h, advance, 5);
  assert.deepEqual([first.body.gain, first.body.levelUp, first.body.growth.affection], [2, true, 6]);
  assert.equal((await play(app, h, advance, 5)).body.gain, 0);   // 14:59:20, 같은 날
  advance(10_000);
  assert.equal((await play(app, h, advance, 5)).body.gain, 2);   // 14:59:30 시작 → 15:00:10(KST 다음 날 0:00) 점수

  advance(DAY_MS);
  await call(app, 'POST', '/games/start', h, { game: 'tidy' });   // 시작만 하고 그만둠
  advance(5 * 60_000);
  assert.equal((await play(app, h, advance, 5)).body.gain, 2);

  advance(DAY_MS);
  db.prepare('UPDATE character SET affection = 174').run();
  const top = await play(app, h, advance, 5);
  assert.deepEqual([top.body.gain, top.body.levelUp, top.body.growth.completed], [1, false, true]);
  advance(DAY_MS);
  assert.equal((await play(app, h, advance, 5)).body.gain, 0);   // 완성

  await call(app, 'POST', '/characters/reset', h, {});
  advance(DAY_MS);
  const none = await play(app, h, advance, 7);
  assert.deepEqual([none.code, none.body.gain, none.body.levelUp, none.body.best], [200, 0, false, 7]);
});

test('이번 달 최고만 남기고 KST 1일 0시에 새 달, 지난달 랭킹은 month 로', async () => {
  const { app, db, advance } = setup('2026-11-30T14:00:00Z');   // KST 11월 30일 23:00
  const h = await signup(app);
  assert.equal((await play(app, h, advance, 300)).body.best, 300);
  assert.equal((await play(app, h, advance, 200)).body.best, 300);
  assert.equal((await play(app, h, advance, 400)).body.best, 400);
  advance(60 * 60_000);   // KST 12월 1일 0:02
  assert.equal((await play(app, h, advance, 50)).body.best, 50);
  const dec = (await call(app, 'GET', '/rankings', h)).body;
  const nov = (await call(app, 'GET', '/rankings?month=2026-11', h)).body;
  assert.deepEqual([dec.month, dec.best, nov.month, nov.best, nov.rank], ['2026-12', 50, '2026-11', 400, 1]);
  assert.equal(db.prepare('SELECT count(*) n FROM score').get().n, 2);
});

test('11월 마지막 판의 응답을 잃고 12월에 다시 보내면 11월 기록을 돌려준다', async () => {
  const { app, advance } = setup('2026-11-30T14:59:00Z');   // KST 11월 30일 23:59:00
  const h = await signup(app);
  const ok = await play(app, h, advance, 300);   // 23:59:40 에 11월로 기록
  advance(25_000);   // KST 12월 1일 0:00:05
  const again = await call(app, 'POST', '/scores', h, { score: 300 });
  assert.deepEqual([again.code, again.body.best, again.body.rank], [200, ok.body.best, 1]);
});

test('순위: 동점은 같은 등수(1, 1, 3) · 다음 등수까지 = 바로 위 점수 − 내 최고 · 1등 0 · 탈퇴하면 빠짐', async () => {
  const { app, db, advance } = setup();
  const h = await signup(app);
  addScore(db, 'a', 100);
  addScore(db, 'b', 80);
  const mid = await play(app, h, advance, 90);
  assert.deepEqual([mid.body.rank, mid.body.gap], [2, 10]);
  const tie = await play(app, h, advance, 100);
  assert.deepEqual([tie.body.rank, tie.body.gap], [1, 0]);

  const b = (await call(app, 'GET', '/rankings', { 'x-player-id': 'b' })).body;
  assert.deepEqual(b.top, [{ rank: 1, nickname: 'a', best: 100 }, { rank: 1, nickname: 'max', best: 100 }, { rank: 3, nickname: 'b', best: 80 }]);
  assert.deepEqual([b.rank, b.best, b.gap], [3, 80, 20]);

  await call(app, 'DELETE', '/players/me', h);
  const after = (await call(app, 'GET', '/rankings', { 'x-player-id': 'b' })).body;
  assert.deepEqual(after.top.map((r) => r.nickname), ['a', 'b']);
  assert.equal(after.rank, 2);
});

test('랭킹: 상위 20줄 · 20위 밖이어도 내 순위 · 출시 전 달과 다음 달은 게임 없음 · event payload', async () => {
  const { app, db, advance } = setup();
  const h = await signup(app);
  for (let i = 0; i < 22; i++) addScore(db, `p${i}`, 200 + i);
  addScore(db, 'early', 999, '2026-10');   // 출시 전 달에 들어간 행이 있어도 보이지 않는다
  await play(app, h, advance, 150);
  const r = (await call(app, 'GET', '/rankings', h)).body;
  assert.equal(r.top.length, 20);
  assert.deepEqual(r.top[0], { rank: 1, nickname: 'p21', best: 221 });
  assert.deepEqual([r.month, r.game, r.rank, r.best, r.gap], ['2026-11', 'tidy', 23, 150, 50]);
  const empty = { game: '', top: [], best: 0, rank: 0, gap: 0 };
  assert.deepEqual((await call(app, 'GET', '/rankings?month=2026-10', h)).body, { month: '2026-10', ...empty });
  assert.deepEqual((await call(app, 'GET', '/rankings?month=2026-12', h)).body, { month: '2026-12', ...empty });

  const events = db.prepare("SELECT type, payload FROM event WHERE type LIKE 'game_%' ORDER BY id").all().map((e) => [e.type, JSON.parse(e.payload)]);
  assert.deepEqual(events, [['game_play', { game: 'tidy' }], ['game_score', { game: 'tidy', score: 150, ms: 40_000 }]]);
});

test('달력 12칸: 올해 1~12월 · 출시 전과 다음 달 이후는 게임 없음 · 내 등수 · 최고, 해가 바뀌면 새 12칸', async () => {
  const { app, db, advance } = setup('2026-11-30T14:00:00Z');   // KST 11월 30일 23:00
  const h = await signup(app);
  addScore(db, 'a', 500);
  await play(app, h, advance, 120);
  advance(60 * 60_000);   // KST 12월 1일
  const { current, months } = (await call(app, 'GET', '/games/months', h)).body;
  assert.equal(current, '2026-12');
  assert.deepEqual(months.map((m) => m.month), Array.from({ length: 12 }, (_, i) => `2026-${String(i + 1).padStart(2, '0')}`));
  assert.deepEqual(months[9], { month: '2026-10', game: '', best: 0, rank: 0 });
  assert.deepEqual(months[10], { month: '2026-11', game: 'tidy', best: 120, rank: 2 });
  assert.deepEqual(months[11], { month: '2026-12', game: 'tidy', best: 0, rank: 0 });   // 이번 달, 아직 안 함

  advance(31 * DAY_MS);   // KST 2027년 1월
  const next = (await call(app, 'GET', '/games/months', h)).body;
  assert.equal(next.current, '2027-01');
  assert.deepEqual([next.months[0].game, next.months[1].game], ['tidy', '']);
});

test('첫 점수(0점 판도)를 받으면 묘약 히든 미션 first_game 달성 · Main 빨간 점, 시작만으로는 아님', async () => {
  const { app, advance } = setup();
  const h = await signup(app);
  await call(app, 'POST', '/games/start', h, { game: 'tidy' });
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, false);
  advance(40_000);
  assert.equal((await call(app, 'POST', '/scores', h, { score: 0 })).code, 200);
  assert.equal((await call(app, 'GET', '/missions', h)).body.hidden.find((m) => m.id === 'first_game').done, true);
  assert.equal((await call(app, 'GET', '/me', h)).body.potionReady, true);
});
