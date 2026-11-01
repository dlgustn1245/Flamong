import { DatabaseSync } from 'node:sqlite';
import { readFileSync } from 'node:fs';

/**
 * @param {string} path ':memory:' 이면 테스트용
 * @returns {DatabaseSync}
 */
export function openDb(path = 'flamong.db') {
  const db = new DatabaseSync(path);
  db.exec('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;');
  db.exec(readFileSync(new URL('./schema.sql', import.meta.url), 'utf8'));
  return db;
}

/**
 * 모든 쓰기 라우트가 호출한다. 대시보드는 이 테이블만 본다.
 * @param {DatabaseSync} db
 * @param {string | null} playerId
 * @param {string} type
 * @param {object | null} payload
 * @param {() => Date} now
 */
export function logEvent(db, playerId, type, payload, now) {
  db.prepare('INSERT INTO event (player_id, type, ts, payload) VALUES (?, ?, ?, ?)')
    .run(playerId, type, now().toISOString(), payload ? JSON.stringify(payload) : null);
}

/**
 * 헤더의 playerId 로 플레이어를 찾는다. 없으면 에러 응답을 보내고 null.
 * 모르는 id 를 404 로 주면 「없는 주소」 404 와 섞인다. 클라이언트는 401 을 받으면 기기의 playerId 를 지운다
 * @param {DatabaseSync} db
 * @param {import('fastify').FastifyRequest} req
 * @param {import('fastify').FastifyReply} reply
 */
export function playerOf(db, req, reply) {
  const id = req.headers['x-player-id'];
  if (!id) {
    reply.code(401).send({ error: 'no_player' });
    return null;
  }
  const player = db.prepare('SELECT id, team, name, nickname, hearts, hearts_date, can_post_news FROM player WHERE id = ?').get(id);
  if (!player) {
    reply.code(401).send({ error: 'unknown_player' });
    return null;
  }
  return player;
}

/**
 * fn 의 쓰기를 한 트랜잭션으로 묶는다. fn 이 던지면 ROLLBACK 후 다시 던진다.
 * @template T
 * @param {DatabaseSync} db
 * @param {() => T} fn
 * @returns {T}
 */
export function tx(db, fn) {
  db.exec('BEGIN');
  try {
    const result = fn();
    db.exec('COMMIT');
    return result;
  } catch (e) {
    db.exec('ROLLBACK');
    throw e;
  }
}
