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
