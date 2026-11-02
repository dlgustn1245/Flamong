import Fastify from 'fastify';
import cors from '@fastify/cors';
import { openDb } from './db.js';
import players from './routes/players.js';
import character from './routes/character.js';
import potion from './routes/potion.js';
import posts from './routes/posts.js';
import { isLimited, pruneHits } from './rules/rateLimit.js';

// 가입 · 닉네임 확인 · 로그인만 막는다. 회사 와이파이는 직원이 모두 같은 IP 로 나가 넉넉하게 잡는다
const LIMITS = {
  'POST /players': { max: 30, windowMs: 10 * 60_000 },
  'GET /players/check': { max: 60, windowMs: 60_000 },
  'POST /players/login': { max: 60, windowMs: 10 * 60_000 },
};

/**
 * 테스트가 인메모리 DB와 고정 시계를 주입할 수 있게 앱 생성을 분리한다.
 * now() 는 여기서 한 번만 만들고 아래로 넘긴다. 규칙 코드에서 new Date() 직접 호출 금지.
 * @param {{ db?: import('node:sqlite').DatabaseSync, now?: () => Date }} opts
 */
export function buildApp({ db = openDb(), now = () => new Date() } = {}) {
  // Caddy 뒤에서 req.ip 가 실제 접속 IP 가 되게. Caddy 는 바깥에서 온 X-Forwarded-For 를 믿지 않고 직접 채운다
  const app = Fastify({ logger: true, trustProxy: '127.0.0.1' });
  // 배포 서버는 Caddy 가 빌드와 API 를 같은 주소로 내보내 CORS 가 필요 없다. 내 PC 에서 띄운 웹 빌드(localhost)만 허용.
  // methods 기본값은 GET·HEAD·POST 뿐이라 PATCH·DELETE 를 브라우저가 막는다
  app.register(cors, { origin: /^http:\/\/localhost(:\d+)?$/, methods: ['GET', 'HEAD', 'POST', 'PATCH', 'DELETE'] });

  app.setErrorHandler((err, req, reply) => {
    const code = err.statusCode ?? 500;
    if (code >= 500) req.log.error(err);
    reply.code(code).send({ error: err.code ?? 'internal_error', message: code >= 500 ? 'internal error' : err.message });
  });
  app.setNotFoundHandler((req, reply) => reply.code(404).send({ error: 'not_found' }));

  // 본문 검사 전에 센다. 잘못된 요청도 세고, 막힌 요청은 비밀번호 해시(scrypt)까지 가지 않는다
  const hits = new Map();
  app.addHook('onRequest', async (req, reply) => {
    const route = `${req.method} ${req.routeOptions.url}`;
    const limit = LIMITS[route];
    if (!limit) return;
    const t = now().getTime();
    if (hits.size > 10_000) pruneHits(hits, t);
    if (isLimited(hits, `${route} ${req.ip}`, limit.max, limit.windowMs, t)) {
      return reply.code(429).send({ error: 'rate_limited' });
    }
  });

  app.register(players, { db, now });
  app.register(character, { db, now });
  app.register(potion, { db, now });
  app.register(posts, { db, now });
  return app;
}

if (process.argv[1].endsWith('index.js')) {
  buildApp().listen({ port: 3000, host: '127.0.0.1' });
}
