import { logEvent, playerOf, tx } from '../db.js';
import { addAffection, growthOf, HIDDEN_MISSIONS, kstDate, levelOf, MAX_AFFECTION, POTION_AFFECTION, rechargeHearts, visitStatus } from '../rules/growth.js';

/**
 * 미션 상태. 달성 여부는 저장하지 않고 다른 테이블에서 계산한다. 받은 기록만 potion 에 있다.
 * 방문 묘약은 mission_id 'visit:1', 'visit:2' … 로 몇 번째인지 적어 UNIQUE 로 두 번 받기를 막는다.
 * GET /missions 와 /me 의 potionReady 가 같이 쓴다.
 * @param {import('node:sqlite').DatabaseSync} db
 * @param {string} playerId
 * @param {{ affection: number } | undefined} character 활성 캐릭터
 */
export function missionsOf(db, playerId, character) {
  const claimed = new Set(db.prepare('SELECT mission_id FROM potion WHERE player_id = ?').all(playerId).map((r) => r.mission_id));
  const visitDays = db.prepare('SELECT count(*) AS n FROM emotion_log WHERE player_id = ?').get(playerId).n;
  const visitClaimed = [...claimed].filter((id) => id.startsWith('visit:')).length;
  const done = {
    first_complete: db.prepare('SELECT 1 FROM character WHERE player_id = ? AND affection >= ?').get(playerId, MAX_AFFECTION),
    first_game: db.prepare('SELECT 1 FROM score WHERE player_id = ?').get(playerId),
    first_garden: db.prepare("SELECT 1 FROM post WHERE player_id = ? AND kind = 'garden'").get(playerId),
  };
  // 캐릭터가 없거나 완성했으면 마실 수 없다. 묘약은 남아 있다가 새로 키울 때 마신다
  const canDrink = Boolean(character) && !levelOf(character.affection).completed;
  const visit = visitStatus(visitDays, visitClaimed);
  const hidden = HIDDEN_MISSIONS.map((id) => ({ id, done: Boolean(done[id]), claimed: claimed.has(id) }));
  const ready = canDrink && (visit.progress >= visit.need || hidden.some((h) => h.done && !h.claimed));
  return { canDrink, visit, hidden, ready, visitClaimed };
}

/**
 * 묘약. 받으면 바로 마셔 애정도 +10. 핸들러는 동기라 읽기 → 계산 → 쓰기 사이에 다른 요청이 끼어들지 못한다.
 * @param {import('fastify').FastifyInstance} app
 * @param {{ db: import('node:sqlite').DatabaseSync, now: () => Date }} opts
 */
export default async function potion(app, { db, now }) {
  const getActive = db.prepare('SELECT id, affection FROM character WHERE player_id = ? AND ended_at IS NULL');
  const setAffection = db.prepare('UPDATE character SET affection = ? WHERE id = ?');
  const insertPotion = db.prepare('INSERT INTO potion (player_id, mission_id, claimed_at) VALUES (?, ?, ?)');

  app.get('/missions', (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const { canDrink, visit, hidden } = missionsOf(db, p.id, getActive.get(p.id));
    return { canDrink, visit, hidden };
  });

  // 응답을 못 받고 다시 보내면 방문은 not_ready(더 받을 것이 없을 때), 히든은 claimed. 클라이언트는 409 면 /missions 를 다시 부른다
  app.post('/potions/claim', {
    schema: {
      body: {
        type: 'object',
        required: ['missionId'],
        properties: { missionId: { type: 'string', enum: ['visit', ...HIDDEN_MISSIONS] } },
      },
    },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const c = getActive.get(p.id);
    if (!c) return reply.code(409).send({ error: 'no_character' });
    if (levelOf(c.affection).completed) return reply.code(409).send({ error: 'completed' });

    const { missionId } = req.body;
    const m = missionsOf(db, p.id, c);
    let key = missionId;
    if (missionId === 'visit') {
      if (m.visit.progress < m.visit.need) return reply.code(409).send({ error: 'not_ready' });
      key = `visit:${m.visitClaimed + 1}`;
    } else {
      const h = m.hidden.find((x) => x.id === missionId);
      if (h.claimed) return reply.code(409).send({ error: 'claimed' });
      if (!h.done) return reply.code(409).send({ error: 'not_ready' });
    }

    const affection = addAffection(c.affection, POTION_AFFECTION);
    tx(db, () => {
      insertPotion.run(p.id, key, now().toISOString());
      setAffection.run(affection, c.id);
      logEvent(db, p.id, 'potion', { missionId: key }, now);
    });
    return {
      levelUp: levelOf(affection).level > levelOf(c.affection).level,
      growth: growthOf(rechargeHearts(p.hearts, p.hearts_date, kstDate(now())), affection),
    };
  });
}
