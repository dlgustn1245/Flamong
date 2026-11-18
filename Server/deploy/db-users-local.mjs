// 로컬 개발 DB(Server/flamong.db)의 사용자 목록. 읽기만 한다.
// 개발용 테스트 계정이라 팀 · 이름까지 보여 준다. 운영 DB 에는 쓰지 않는다(운영은 db-stats.bat).
// 비밀번호 해시는 찍지 않고, playerId 는 앞 8자만 찍는다
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { DatabaseSync } from 'node:sqlite';
import { kstDate, levelOf, MAX_AFFECTION, rechargeHearts } from '../rules/growth.js';
import { SPECIES_PER_EGG } from '../rules/species.js';

const FILE = fileURLToPath(new URL('../flamong.db', import.meta.url));
if (!existsSync(FILE)) {
  console.log(`로컬 DB 가 없습니다: ${FILE}\nServer 폴더에서 npm start 를 한 번 실행하면 만들어집니다.`);
  process.exit(0);
}
const db = new DatabaseSync(FILE, { readOnly: true });
const one = (sql, ...args) => db.prepare(sql).get(...args);
const EGGS = ['핑크', '퍼플', '블루', '그린', '그레이', '오렌지', '라벤더', '크림'];
const today = kstDate(new Date());
const month = today.slice(0, 7);

const players = db.prepare('SELECT id, team, name, nickname, can_post_news, created_at, hearts, hearts_date FROM player ORDER BY created_at').all();
console.log(`로컬 DB ${FILE}`);
console.log(`오늘 ${today} (KST) · 사용자 ${players.length}명\n`);

players.forEach((p, i) => {
  const c = one('SELECT species_id, affection FROM character WHERE player_id = ? AND ended_at IS NULL', p.id);
  const done = one('SELECT count(*) n FROM character WHERE player_id = ? AND affection >= ?', p.id, MAX_AFFECTION).n;
  const days = one('SELECT count(*) n FROM emotion_log WHERE player_id = ?', p.id).n;
  const last = one('SELECT date, emotion FROM emotion_log WHERE player_id = ? ORDER BY date DESC LIMIT 1', p.id);
  const best = one('SELECT MAX(best_score) top FROM score WHERE player_id = ? AND month = ?', p.id, month).top;
  const potions = one('SELECT count(*) n FROM potion WHERE player_id = ?', p.id).n;
  const posts = one('SELECT count(*) n FROM post WHERE player_id = ?', p.id).n;

  console.log(`[${i + 1}] ${p.nickname}  (팀 ${p.team} · 이름 ${p.name})  가입 ${kstDate(new Date(p.created_at))}  id ${p.id.slice(0, 8)}…`);
  const character = c
    ? `종 ${c.species_id}(${EGGS[Math.floor(c.species_id / SPECIES_PER_EGG)]} 알) Lv${levelOf(c.affection).level} 애정도 ${c.affection}/${MAX_AFFECTION}`
    : '없음(알을 고르기 전)';
  console.log(`    캐릭터: ${character} · 완성 ${done}마리 · 하트 ${rechargeHearts(p.hearts, p.hearts_date, today)}개`);
  console.log(`    감정 ${days}일${last ? `(마지막 ${last.date} ${last.emotion})` : ''} · ${month} 게임 최고 ${best ?? '-'}점 · 묘약 ${potions}개 · 글 ${posts}개 · 소식 작성권 ${p.can_post_news ? '있음' : '없음'}`);
});
