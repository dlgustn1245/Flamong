import { createHash } from 'node:crypto';
import { logEvent, playerOf, tx } from '../db.js';
import { DAY_MS, kstDate } from '../rules/growth.js';
import { isValidText, POST_MAX } from '../rules/text.js';

const KINDS = ['garden', 'news'];
const POST_LIST = 30;   // 탭마다 최신 글 수. 넘기기는 없다

/**
 * 오늘의 소식 담당 1명. 저장하지 않고 날짜로 계산한다.
 * 후보는 어제까지 7일 안에 오늘의 감정을 고른 사람 중 작성권(can_post_news)이 없는 사람. 오늘을 빼서 하루 동안 결과가 바뀌지 않는다.
 * ponytail: 그날 후보 중 누가 탈퇴하면 남은 사람 중에서 다시 뽑힌다. 드물어서 저장하지 않음
 * @param {import('node:sqlite').DatabaseSync} db
 * @param {string} today KST YYYY-MM-DD
 * @returns {string | null} playerId
 */
export function newsPickOf(db, today) {
  const from = new Date(Date.parse(today) - 7 * DAY_MS).toISOString().slice(0, 10);
  const candidates = db.prepare(`SELECT DISTINCT e.player_id AS id FROM emotion_log e JOIN player p ON p.id = e.player_id
    WHERE e.date >= ? AND e.date < ? AND p.can_post_news = 0 ORDER BY e.player_id`).all(from, today);
  if (candidates.length === 0) return null;
  return candidates[createHash('sha256').update(today).digest().readUInt32BE(0) % candidates.length].id;
}

/**
 * Main 확성기 빨간 점. 오늘의 담당이고 아직 소식을 안 썼는지
 * @param {import('node:sqlite').DatabaseSync} db @param {string} playerId @param {string} today
 */
export function newsReadyOf(db, playerId, today) {
  return newsPickOf(db, today) === playerId
    && !db.prepare("SELECT 1 FROM post WHERE player_id = ? AND date = ? AND kind = 'news'").get(playerId, today);
}

/**
 * 꽃밭(익명) · 소식(닉네임). 하루 1회는 UNIQUE(player_id, date, kind) 로 막는다. 작성자는 둘 다 저장한다(신고 대응)
 * @param {import('fastify').FastifyInstance} app
 * @param {{ db: import('node:sqlite').DatabaseSync, now: () => Date }} opts
 */
export default async function posts(app, { db, now }) {
  const getPost = db.prepare('SELECT 1 FROM post WHERE player_id = ? AND date = ? AND kind = ?');
  const insertPost = db.prepare('INSERT INTO post (player_id, date, kind, body) VALUES (?, ?, ?, ?)');
  const listPosts = db.prepare(`SELECT post.body, post.date, player.nickname FROM post JOIN player ON player.id = post.player_id
    WHERE post.kind = ? ORDER BY post.id DESC LIMIT ?`);
  const hasNewsRight = (p, today) => p.can_post_news === 1 || newsPickOf(db, today) === p.id;

  app.get('/posts', {
    schema: {
      querystring: {
        type: 'object',
        required: ['kind'],
        properties: { kind: { type: 'string', enum: KINDS } },
      },
    },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const { kind } = req.query;
    const today = kstDate(now());
    const canWrite = !getPost.get(p.id, today, kind) && (kind === 'garden' || hasNewsRight(p, today));
    // 꽃밭은 익명이라 닉네임을 보내지 않는다. JsonUtility 가 null 대신 빈 문자열을 받게 '' 로 채운다
    const list = listPosts.all(kind, POST_LIST).map((r) => ({ body: r.body, date: r.date, nickname: kind === 'news' ? r.nickname : '' }));
    return { canWrite, posts: list };
  });

  app.post('/posts', {
    schema: {
      body: {
        type: 'object',
        required: ['kind', 'body'],
        properties: {
          kind: { type: 'string', enum: KINDS },
          body: { type: 'string', maxLength: POST_MAX },
        },
      },
    },
  }, (req, reply) => {
    const p = playerOf(db, req, reply);
    if (!p) return reply;
    const { kind } = req.body;
    const body = req.body.body.trim();
    if (!isValidText(body, POST_MAX)) return reply.code(400).send({ error: 'body_charset' });
    const today = kstDate(now());
    if (kind === 'news' && !hasNewsRight(p, today)) return reply.code(409).send({ error: 'no_right' });

    try {
      tx(db, () => {
        const { lastInsertRowid } = insertPost.run(p.id, today, kind, body);   // 오늘 두 번째면 UNIQUE 위반
        logEvent(db, p.id, kind === 'garden' ? 'post' : 'news', { id: Number(lastInsertRowid) }, now);
      });
    } catch (e) {
      if (String(e.message).includes('UNIQUE')) return reply.code(409).send({ error: 'posted' });
      throw e;
    }
    return { ok: true };
  });
}
