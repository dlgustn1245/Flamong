import Fastify from 'fastify';
import cors from '@fastify/cors';
import { openDb } from './db.js';
import players from './routes/players.js';

/**
 * 테스트가 인메모리 DB와 고정 시계를 주입할 수 있게 앱 생성을 분리한다.
 * now() 는 여기서 한 번만 만들고 아래로 넘긴다. 규칙 코드에서 new Date() 직접 호출 금지.
 * @param {{ db?: import('node:sqlite').DatabaseSync, now?: () => Date }} opts
 */
export function buildApp({ db = openDb(), now = () => new Date() } = {}) {
  const app = Fastify({ logger: true });
  // 기본값은 GET·HEAD·POST 뿐이라 PATCH·DELETE 를 브라우저가 막는다
  app.register(cors, { origin: true, methods: ['GET', 'HEAD', 'POST', 'PATCH', 'DELETE'] }); // ponytail: 개발용 전체 허용. 배포 시 도메인 지정

  // 모든 응답을 { statusCode, body } 로 통일. HTTP 상태 코드도 같은 값으로 유지
  app.addHook('preSerialization', async (req, reply, payload) => ({ statusCode: reply.statusCode, body: payload }));
  app.setErrorHandler((err, req, reply) => {
    const code = err.statusCode ?? 500;
    if (code >= 500) req.log.error(err);
    reply.code(code).send({ error: err.code ?? 'internal_error', message: code >= 500 ? 'internal error' : err.message });
  });
  app.setNotFoundHandler((req, reply) => reply.code(404).send({ error: 'not_found' }));

  app.register(players, { db, now });
  return app;
}

if (process.argv[1].endsWith('index.js')) {
  buildApp().listen({ port: 3000, host: '127.0.0.1' });
}
