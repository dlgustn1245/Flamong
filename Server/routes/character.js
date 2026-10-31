import { logEvent, playerOf, tx } from '../db.js';
import { addAffection, EMOTION_AFFECTION, EMOTIONS, growthOf, kstDate, levelOf, MAX_AFFECTION, rechargeHearts } from '../rules/growth.js';
import { EGG_COUNT, pickSpecies, SPECIES_PER_EGG } from '../rules/species.js';
import { missionsOf } from './potion.js';

/**
 * 내 캐릭터 · 하트 · 오늘의 감정. 모든 경로가 X-Player-Id 헤더를 요구한다.
 * 핸들러는 전부 동기 함수다. 읽기 → 계산 → 쓰기 사이에 await 가 없어 다른 요청이 끼어들지 못한다.
 * @param {import('fastify').FastifyInstance} app
 * @param {{ db: import('node:sqlite').DatabaseSync, now: () => Date }} opts
 */
export default async function character(app, { db, now }) {
  const getActive = db.prepare('SELECT id, species_id, affection FROM character WHERE player_id = ? AND ended_at IS NULL');
  const getEmotion = db.prepare('SELECT 1 FROM emotion_log WHERE player_id = ? AND date = ?');
  const insertCharacter = db.prepare('INSERT INTO character (player_id, species_id, started_at) VALUES (?, ?, ?)');
  const insertEmotion = db.prepare('INSERT INTO emotion_log (player_id, date, emotion) VALUES (?, ?, ?)');
  const setHearts = db.prepare('UPDATE player SET hearts = ?, hearts_date = ? WHERE id = ?');
  const setAffection = db.prepare('UPDATE character SET affection = ? WHERE id = ?');
  const endCharacter = db.prepare('UPDATE character SET ended_at = ? WHERE id = ?');
  // 종마다 가장 많이 키운 애정도. 지금 캐릭터와 완성한 캐릭터만 센다(완성 전에 새로 키운 캐릭터는 도감에 남지 않는다)
  const getBest = db.prepare(`SELECT species_id, MAX(affection) AS affection FROM character
    WHERE player_id = ? AND (ended_at IS NULL OR affection >= ?) GROUP BY species_id ORDER BY species_id`);

  app.get('/me', (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const today = kstDate(now());
    const c = getActive.get(p.id);
    return {
      team: p.team,
      name: p.name,
      nickname: p.nickname,
      speciesId: c ? c.species_id : -1,   // -1: 아직 알을 고르지 않음. JsonUtility 는 int 자리의 null 을 0 으로 읽는다
      emotionToday: Boolean(getEmotion.get(p.id, today)),
      growth: growthOf(rechargeHearts(p.hearts, p.hearts_date, today), c ? c.affection : 0),
      potionReady: missionsOf(db, p.id, c).ready,   // Main 묘약 버튼의 빨간 점
    };
  });

  app.post('/characters', {
    schema: {
      body: {
        type: 'object',
        required: ['eggId'],
        properties: { eggId: { type: 'integer', minimum: 0, maximum: EGG_COUNT - 1 } },
      },
    },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    // 이미 있으면 새로 뽑지 않고 그 캐릭터를 돌려준다.
    // 응답을 못 받은(시간 초과) 클라이언트가 재시도해도 같은 결과가 오고, 다시 뽑기도 막힌다
    const existing = getActive.get(p.id);
    if (existing) return { speciesId: existing.species_id };

    const { eggId } = req.body;
    const speciesId = pickSpecies(eggId);
    insertCharacter.run(p.id, speciesId, now().toISOString());
    logEvent(db, p.id, 'hatch', { eggId, speciesId }, now);
    return { speciesId };
  });

  // 새로 키우기. 지금 캐릭터는 ended_at 을 적어 보관만 하고, 새 캐릭터는 알을 다시 골라 POST /characters 로 만든다.
  // 그 사이 /me 의 speciesId 가 -1 이라 앱을 껐다 켜도 EggSelect 로 간다. 하트는 플레이어 것이라 그대로 둔다.
  // 활성 캐릭터가 없으면(응답을 못 받고 재시도) 이미 끝난 것이므로 그대로 ok
  app.post('/characters/reset', (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const c = getActive.get(p.id);
    if (!c) return { ok: true };
    tx(db, () => {
      endCharacter.run(now().toISOString(), c.id);
      logEvent(db, p.id, 'reset', { speciesId: c.species_id, affection: c.affection, completed: levelOf(c.affection).completed }, now);
    });
    return { ok: true };
  });

  // 도감. 새로 키우기를 해도 캐릭터 행을 지우지 않으므로 따로 저장할 것이 없다.
  // levels[speciesId] = 도달한 최고 레벨(0 = 아직 없음), completed = 완성한 종, speciesId = 지금 캐릭터(없으면 -1)
  app.get('/collection', (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const levels = new Array(EGG_COUNT * SPECIES_PER_EGG).fill(0);
    const completed = [];
    for (const row of getBest.all(p.id, MAX_AFFECTION)) {
      levels[row.species_id] = levelOf(row.affection).level;
      if (row.affection >= MAX_AFFECTION) completed.push(row.species_id);
    }
    const c = getActive.get(p.id);
    return { levels, completed, speciesId: c ? c.species_id : -1 };
  });

  // 하트 1개 사용. 터치 10번은 클라이언트 연출이고 서버는 저장량만 본다
  app.post('/tap', (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const c = getActive.get(p.id);
    if (!c) return reply.code(409).send({ error: 'no_character' });
    const before = levelOf(c.affection);
    if (before.completed) return reply.code(409).send({ error: 'completed' });

    const today = kstDate(now());
    const hearts = rechargeHearts(p.hearts, p.hearts_date, today) - 1;
    if (hearts < 0) return reply.code(409).send({ error: 'no_hearts' });
    const affection = addAffection(c.affection, 1);

    tx(db, () => {
      setHearts.run(hearts, today, p.id);
      setAffection.run(affection, c.id);
      logEvent(db, p.id, 'tap', null, now);
    });
    return { levelUp: levelOf(affection).level > before.level, growth: growthOf(hearts, affection) };
  });

  // 완성 뒤에도 기록은 남긴다 (대시보드 데이터). 애정도는 addAffection 이 175 에서 막는다
  app.post('/emotions', {
    schema: {
      body: {
        type: 'object',
        required: ['emotion'],
        properties: { emotion: { type: 'string', enum: EMOTIONS } },
      },
    },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const c = getActive.get(p.id);
    if (!c) return reply.code(409).send({ error: 'no_character' });

    const { emotion } = req.body;
    const today = kstDate(now());
    const affection = addAffection(c.affection, EMOTION_AFFECTION);
    try {
      tx(db, () => {
        insertEmotion.run(p.id, today, emotion);   // 오늘 두 번째면 UNIQUE 위반 → 애정도도 함께 되돌린다
        setAffection.run(affection, c.id);
        logEvent(db, p.id, 'emotion', { emotion }, now);
      });
    } catch (e) {
      if (String(e.message).includes('UNIQUE')) return reply.code(409).send({ error: 'emotion_done' });
      throw e;
    }
    return {
      levelUp: levelOf(affection).level > levelOf(c.affection).level,
      growth: growthOf(rechargeHearts(p.hearts, p.hearts_date, today), affection),
    };
  });
}
