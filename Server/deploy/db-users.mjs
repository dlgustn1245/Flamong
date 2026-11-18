// 사용자 목록. 읽기만 한다. 로컬 개발 DB 와 운영 DB 에 같이 쓴다
//   로컬: db-users-local.bat → node deploy/db-users.mjs (Server/flamong.db, 팀 · 이름까지 보임)
//   운영: db-users.bat → ssh 로 서버의 node 에 이 파일을 넘긴다 (FLAMONG_SERVER · FLAMONG_DB 로 위치를 알려 준다)
// 팀 · 이름(실명)과 playerId 앞 8자는 SHOW_NAMES=1 일 때만 찍는다. 비밀번호 해시는 어떤 경우에도 찍지 않는다
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { DatabaseSync } from 'node:sqlite';

// 운영에서는 stdin 으로 들어와 이 파일의 위치를 알 수 없으므로 환경 변수로 받는다
const SERVER = process.env.FLAMONG_SERVER ?? fileURLToPath(new URL('..', import.meta.url));
const FILE = process.env.FLAMONG_DB ?? join(SERVER, 'flamong.db');
const SHOW_NAMES = process.env.SHOW_NAMES === '1';
const { kstDate, levelOf, MAX_AFFECTION, rechargeHearts } = await import(pathToFileURL(join(SERVER, 'rules/growth.js')));
const { SPECIES_PER_EGG } = await import(pathToFileURL(join(SERVER, 'rules/species.js')));

if (!existsSync(FILE)) {
  console.log(`DB 가 없습니다: ${FILE}\n로컬이라면 Server 폴더에서 npm start 를 한 번 실행하면 만들어집니다.`);
  process.exit(0);
}
const db = new DatabaseSync(FILE, { readOnly: true });
const one = (sql, ...args) => db.prepare(sql).get(...args);
const EGGS = ['핑크', '퍼플', '블루', '그린', '그레이', '오렌지', '라벤더', '크림'];
const today = kstDate(new Date());
const month = today.slice(0, 7);

const players = db.prepare('SELECT id, team, name, nickname, can_post_news, created_at, hearts, hearts_date FROM player ORDER BY created_at').all();
console.log(`DB ${FILE}`);
console.log(`오늘 ${today} (KST) · 사용자 ${players.length}명\n`);

players.forEach((p, i) => {
  const c = one('SELECT species_id, affection FROM character WHERE player_id = ? AND ended_at IS NULL', p.id);
  const done = one('SELECT count(*) n FROM character WHERE player_id = ? AND affection >= ?', p.id, MAX_AFFECTION).n;
  const days = one('SELECT count(*) n FROM emotion_log WHERE player_id = ?', p.id).n;
  const last = one('SELECT date, emotion FROM emotion_log WHERE player_id = ? ORDER BY date DESC LIMIT 1', p.id);
  const best = one('SELECT MAX(best_score) top FROM score WHERE player_id = ? AND month = ?', p.id, month).top;
  const potions = one('SELECT count(*) n FROM potion WHERE player_id = ?', p.id).n;
  const posts = one('SELECT count(*) n FROM post WHERE player_id = ?', p.id).n;

  const who = SHOW_NAMES ? `  (팀 ${p.team} · 이름 ${p.name})` : '';
  const id = SHOW_NAMES ? `  id ${p.id.slice(0, 8)}…` : '';
  console.log(`[${i + 1}] ${p.nickname}${who}  가입 ${kstDate(new Date(p.created_at))}${id}`);
  const character = c
    ? `종 ${c.species_id}(${EGGS[Math.floor(c.species_id / SPECIES_PER_EGG)]} 알) Lv${levelOf(c.affection).level} 애정도 ${c.affection}/${MAX_AFFECTION}`
    : '없음(알을 고르기 전)';
  console.log(`    캐릭터: ${character} · 완성 ${done}마리 · 하트 ${rechargeHearts(p.hearts, p.hearts_date, today)}개`);
  console.log(`    감정 ${days}일${last ? `(마지막 ${last.date} ${last.emotion})` : ''} · ${month} 게임 최고 ${best ?? '-'}점 · 묘약 ${potions}개 · 글 ${posts}개 · 소식 작성권 ${p.can_post_news ? '있음' : '없음'}`);
});
