import { randomUUID } from 'node:crypto';
import { logEvent } from '../db.js';
import { isValidNickname, NICKNAME_MAX } from '../rules/nickname.js';

/**
 * @param {import('fastify').FastifyInstance} app
 * @param {{ db: import('node:sqlite').DatabaseSync, now: () => Date }} opts
 */
export default async function players(app, { db, now }) {
  app.get('/players/check', {
    schema: {
      querystring: {
        type: 'object',
        required: ['nickname'],
        properties: { nickname: { type: 'string', maxLength: NICKNAME_MAX } },
      },
    },
  }, (req) => {
    const { nickname } = req.query;
    const taken = db.prepare('SELECT 1 FROM player WHERE nickname = ?').get(nickname);
    return { available: isValidNickname(nickname) && !taken };
  });

  app.post('/players', {
    schema: {
      body: {
        type: 'object',
        required: ['team', 'name', 'nickname'],
        properties: {
          team: { type: 'string', minLength: 1, maxLength: 20 },
          name: { type: 'string', minLength: 1, maxLength: 20 },
          nickname: { type: 'string', minLength: 1, maxLength: NICKNAME_MAX },
        },
      },
    },
  }, (req, reply) => {
    const { team, name, nickname } = req.body;
    if (!isValidNickname(nickname)) return reply.code(400).send({ error: 'nickname_charset' });
    const id = randomUUID();
    try {
      db.prepare('INSERT INTO player (id, team, name, nickname, created_at) VALUES (?, ?, ?, ?, ?)')
        .run(id, team, name, nickname, now().toISOString());
    } catch (e) {
      if (String(e.message).includes('UNIQUE')) return reply.code(409).send({ error: 'nickname_taken' });
      throw e;
    }
    logEvent(db, id, 'login', { team }, now);
    return { playerId: id };
  });

  app.patch('/players/me', {
    schema: {
      body: {
        type: 'object',
        properties: {
          team: { type: 'string', minLength: 1, maxLength: 20 },
          name: { type: 'string', minLength: 1, maxLength: 20 },
        },
      },
    },
  }, (req, reply) => {
    const id = req.headers['x-player-id'];
    if (!id) return reply.code(401).send({ error: 'no_player' });
    const cur = db.prepare('SELECT team, name FROM player WHERE id = ?').get(id);
    if (!cur) return reply.code(404).send({ error: 'not_found' });
    db.prepare('UPDATE player SET team = ?, name = ? WHERE id = ?')
      .run(req.body.team ?? cur.team, req.body.name ?? cur.name, id);
    return { ok: true };
  });

  // 탈퇴. event 는 남긴다 (탈퇴자 포함 활동 통계가 대시보드 목적)
  app.delete('/players/me', (req, reply) => {
    const id = req.headers['x-player-id'];
    if (!id) return reply.code(401).send({ error: 'no_player' });
    if (!db.prepare('SELECT 1 FROM player WHERE id = ?').get(id)) return reply.code(404).send({ error: 'not_found' });
    db.exec('BEGIN');
    try {
      for (const t of ['character', 'daily_tap', 'score', 'post', 'potion']) {
        db.prepare(`DELETE FROM ${t} WHERE player_id = ?`).run(id);
      }
      db.prepare('DELETE FROM player WHERE id = ?').run(id);
      logEvent(db, id, 'delete', null, now);
      db.exec('COMMIT');
    } catch (e) {
      db.exec('ROLLBACK');
      throw e;
    }
    return { ok: true };
  });
}
