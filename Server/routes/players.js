import { randomUUID } from 'node:crypto';
import { logEvent, playerOf, tx } from '../db.js';
import { isValidNickname, NICKNAME_MAX } from '../rules/nickname.js';
import { HEARTS_START, kstDate } from '../rules/growth.js';
import { hashPassword, isLocked, lockUntil, MAX_LOGIN_FAILS, PASSWORD_PATTERN, verifyPassword } from '../rules/password.js';

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
        required: ['team', 'name', 'nickname', 'password'],
        properties: {
          team: { type: 'string', minLength: 1, maxLength: 20 },
          name: { type: 'string', minLength: 1, maxLength: 20 },
          nickname: { type: 'string', minLength: 1, maxLength: NICKNAME_MAX },
          password: { type: 'string', pattern: PASSWORD_PATTERN },
        },
      },
    },
  }, (req, reply) => {
    const { team, name, nickname, password } = req.body;
    if (!isValidNickname(nickname)) return reply.code(400).send({ error: 'nickname_charset' });
    const id = randomUUID();
    try {
      db.prepare('INSERT INTO player (id, team, name, nickname, created_at, hearts, hearts_date, password_hash) VALUES (?, ?, ?, ?, ?, ?, ?, ?)')
        .run(id, team, name, nickname, now().toISOString(), HEARTS_START, kstDate(now()), hashPassword(password));
    } catch (e) {
      if (String(e.message).includes('UNIQUE')) return reply.code(409).send({ error: 'nickname_taken' });
      throw e;
    }
    logEvent(db, id, 'login', { team }, now);
    return { playerId: id };
  });

  // 다른 기기 · 브라우저에서 다시 들어올 때. 없는 닉네임도 틀린 비밀번호와 같은 답을 준다
  app.post('/players/login', {
    schema: {
      body: {
        type: 'object',
        required: ['nickname', 'password'],
        properties: {
          nickname: { type: 'string', minLength: 1, maxLength: NICKNAME_MAX },
          password: { type: 'string', minLength: 1, maxLength: 6 },
        },
      },
    },
  }, (req, reply) => {
    const { nickname, password } = req.body;
    const p = db.prepare('SELECT id, password_hash, login_fails, locked_until FROM player WHERE nickname = ?').get(nickname);
    if (!p) return reply.code(401).send({ error: 'login_failed' });
    const t = now();
    if (isLocked(p.locked_until, t)) return reply.code(429).send({ error: 'locked' });

    if (!verifyPassword(password, p.password_hash)) {
      const fails = p.login_fails + 1;
      logEvent(db, p.id, 'login_fail', { fails }, now);
      if (fails >= MAX_LOGIN_FAILS) {
        // 잠그면서 횟수는 0 으로. 잠금이 풀린 뒤 다시 5번의 기회
        db.prepare('UPDATE player SET login_fails = 0, locked_until = ? WHERE id = ?').run(lockUntil(t), p.id);
        return reply.code(429).send({ error: 'locked' });
      }
      db.prepare('UPDATE player SET login_fails = ? WHERE id = ?').run(fails, p.id);
      return reply.code(401).send({ error: 'login_failed' });
    }

    db.prepare('UPDATE player SET login_fails = 0, locked_until = NULL WHERE id = ?').run(p.id);
    logEvent(db, p.id, 'login', null, now);
    return { playerId: p.id };
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
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const team = req.body.team ?? p.team;
    db.prepare('UPDATE player SET team = ?, name = ? WHERE id = ?').run(team, req.body.name ?? p.name, p.id);
    logEvent(db, p.id, 'profile', { team }, now);
    return { ok: true };
  });

  // 탈퇴. event 는 남긴다 (탈퇴자 포함 활동 통계가 대시보드 목적)
  app.delete('/players/me', (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    tx(db, () => {
      for (const t of ['character', 'emotion_log', 'score', 'post', 'potion']) {
        db.prepare(`DELETE FROM ${t} WHERE player_id = ?`).run(p.id);
      }
      db.prepare('DELETE FROM player WHERE id = ?').run(p.id);
      logEvent(db, p.id, 'delete', null, now);
    });
    return { ok: true };
  });
}
