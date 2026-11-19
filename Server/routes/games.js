import { logEvent, playerOf, tx } from '../db.js';
import { addAffection, GAME_AFFECTION, growthOf, kstDate, levelOf, rechargeHearts } from '../rules/growth.js';
import { GAMES, gameOfMonth, MAX_PLAY_MS, MAX_SCORE, minPlayMs, RANKING_TOP } from '../rules/game.js';

const MONTH = '^\\d{4}-(0[1-9]|1[0-2])$';

/**
 * 미니게임 · 랭킹. 「한 번 시작에 점수 한 번」은 새 표 없이 event 의 game_play · game_score 로 판정한다.
 * 핸들러는 전부 동기 함수다. 읽기 → 판정 → 쓰기 사이에 await 가 없어 다른 요청이 끼어들지 못한다.
 * @param {import('fastify').FastifyInstance} app
 * @param {{ db: import('node:sqlite').DatabaseSync, now: () => Date }} opts
 */
export default async function games(app, { db, now }) {
  const getActive = db.prepare('SELECT id, affection FROM character WHERE player_id = ? AND ended_at IS NULL');
  const setAffection = db.prepare('UPDATE character SET affection = ? WHERE id = ?');
  const getLastGame = db.prepare("SELECT type, ts, payload FROM event WHERE player_id = ? AND type IN ('game_play', 'game_score') ORDER BY id DESC LIMIT 1");
  const getLastScore = db.prepare("SELECT ts FROM event WHERE player_id = ? AND type = 'game_score' ORDER BY id DESC LIMIT 1");
  // 낮은 점수는 아무것도 바꾸지 않는다
  const upsertScore = db.prepare(`INSERT INTO score (player_id, game, month, best_score) VALUES (?, ?, ?, ?)
    ON CONFLICT (player_id, game, month) DO UPDATE SET best_score = excluded.best_score WHERE excluded.best_score > score.best_score`);
  const getBest = db.prepare('SELECT best_score FROM score WHERE player_id = ? AND game = ? AND month = ?');
  const getAbove = db.prepare('SELECT count(*) AS n, MIN(best_score) AS next FROM score WHERE game = ? AND month = ? AND best_score > ?');
  // 동점은 같은 등수(1, 1, 3). 목록 안에서는 그달 먼저 참여한 사람이 위
  // ponytail: 그달 행을 모두 훑는다. 수만 행이 되면 (game, month, best_score) 인덱스
  const getTop = db.prepare(`SELECT RANK() OVER (ORDER BY s.best_score DESC) AS rank, p.nickname, s.best_score AS best
    FROM score s JOIN player p ON p.id = s.player_id WHERE s.game = ? AND s.month = ? ORDER BY s.best_score DESC, s.rowid LIMIT ?`);

  // 내 그달 최고 · 등수 · 다음 등수까지 점수(바로 위 점수 − 내 최고, 1등이면 0). 기록이 없으면 모두 0
  const standingOf = (playerId, game, month) => {
    const mine = getBest.get(playerId, game, month);
    if (!mine) return { best: 0, rank: 0, gap: 0 };
    const above = getAbove.get(game, month, mine.best_score);
    return { best: mine.best_score, rank: above.n + 1, gap: above.n ? above.next - mine.best_score : 0 };
  };
  // 그달 이달의 게임. 다음 달 이후는 미리 알리지 않고, 출시 전 달은 게임이 없다. 둘 다 '' (JsonUtility 가 null 대신 받게)
  const shownGame = (month, current) => (month <= current && gameOfMonth(month)) || '';

  // 새 시작은 앞의 시작을 덮는다. 점수는 마지막 시작 한 번에 한 번
  // ponytail: 시작은 횟수 제한이 없다(PATCH /players/me 처럼). 도배가 실제로 생기면 LIMITS 에 플레이어별로 넣는다
  app.post('/games/start', {
    schema: {
      body: {
        type: 'object',
        required: ['game'],
        properties: { game: { type: 'string', enum: Object.keys(GAMES) } },
      },
    },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const { game } = req.body;
    logEvent(db, p.id, 'game_play', { game }, now);
    return { rules: GAMES[game] };
  });

  // 점수가 들어가는 달 · 날은 점수를 받은 서버 시각(KST)이다. 지난달 랭킹은 1일 0시에 확정된다
  app.post('/scores', {
    schema: {
      body: {
        type: 'object',
        required: ['score'],
        // 상한은 게임마다 달라 핸들러에서 MAX_SCORE 로 본다
        properties: { score: { type: 'integer', minimum: 0 } },
      },
    },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const t = now();
    const today = kstDate(t);
    const month = today.slice(0, 7);
    const hearts = rechargeHearts(p.hearts, p.hearts_date, today);
    const c = getActive.get(p.id);
    const before = c ? c.affection : 0;

    const last = getLastGame.get(p.id);
    if (!last) return reply.code(409).send({ error: 'not_started' });
    const { game } = JSON.parse(last.payload);
    // 응답을 못 받고 다시 보낸 점수. 첫 요청이 이미 기록했으니 쓰지 않고 그 기록이 들어간 달의 기록을 돌려준다(그 사이 달이 바뀌어도)
    if (last.type === 'game_score') return { ...standingOf(p.id, game, kstDate(new Date(last.ts)).slice(0, 7)), gain: 0, levelUp: false, growth: growthOf(hearts, before) };
    const { score } = req.body;
    if (score > MAX_SCORE[game]) return reply.code(400).send({ error: 'score_range' });
    const ms = t.getTime() - Date.parse(last.ts);
    if (ms < minPlayMs(GAMES[game])) return reply.code(409).send({ error: 'too_fast' });
    if (ms > MAX_PLAY_MS) return reply.code(409).send({ error: 'expired' });

    // 하루 첫 판: 오늘(KST) 받은 점수가 없다. 시작만 하고 그만둔 판은 세지 않는다
    const prev = getLastScore.get(p.id);
    const first = !prev || kstDate(new Date(prev.ts)) !== today;
    const affection = c && first ? addAffection(before, GAME_AFFECTION) : before;
    tx(db, () => {
      upsertScore.run(p.id, game, month, score);
      if (affection !== before) setAffection.run(affection, c.id);
      logEvent(db, p.id, 'game_score', { game, score, ms }, now);
    });
    return {
      ...standingOf(p.id, game, month),
      gain: affection - before,
      levelUp: levelOf(affection).level > levelOf(before).level,
      growth: growthOf(hearts, affection),
    };
  });

  // 한 달의 랭킹. month 를 빼면 이번 달(KST). 달은 기기 시계가 아니라 서버가 정한다
  app.get('/rankings', {
    schema: { querystring: { type: 'object', properties: { month: { type: 'string', pattern: MONTH } } } },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const current = kstDate(now()).slice(0, 7);
    const month = req.query.month ?? current;
    const game = shownGame(month, current);
    if (!game) return { month, game, top: [], best: 0, rank: 0, gap: 0 };
    return { month, game, top: getTop.all(game, month, RANKING_TOP), ...standingOf(p.id, game, month) };
  });

  // 「이달의 게임」 달력 12칸: 올해(KST) 1~12월의 게임과 내 기록. JsonUtility 가 최상위 배열을 못 읽어 객체로 감싼다
  app.get('/games/months', (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const current = kstDate(now()).slice(0, 7);
    const months = [];
    for (let m = 1; m <= 12; m++) {
      const month = `${current.slice(0, 4)}-${String(m).padStart(2, '0')}`;
      const game = shownGame(month, current);
      const { best, rank } = game ? standingOf(p.id, game, month) : { best: 0, rank: 0 };
      months.push({ month, game, best, rank });
    }
    return { current, months };
  });
}
