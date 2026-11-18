// 운영 DB 현황. 읽기만 하고, 실명 · playerId 는 찍지 않는다.
// 서버에서 도는 스크립트다. PC 에서는 db-stats.bat 이 ssh 로 서버의 node 에 넘긴다
import { statSync } from 'node:fs';
import { DatabaseSync } from 'node:sqlite';
import { EMOTIONS, kstDate, levelOf, MAX_AFFECTION } from '/opt/flamong/server/rules/growth.js';

const FILE = '/var/lib/flamong/flamong.db';
const db = new DatabaseSync(FILE, { readOnly: true });
const one = (sql, ...args) => db.prepare(sql).get(...args);
const all = (sql, ...args) => db.prepare(sql).all(...args);

const now = new Date();
const daysAgo = (n) => new Date(now.getTime() - n * 86_400_000);
const today = kstDate(now);
const yesterday = kstDate(daysAgo(1));
const weekAgo = kstDate(daysAgo(7));
const month = today.slice(0, 7);

console.log(`오늘 ${today} (KST)`);

console.log(`\n[가입자] 전체 ${one('SELECT count(*) n FROM player').n}명 · 최근 7일 가입 ${one('SELECT count(*) n FROM player WHERE created_at >= ?', daysAgo(7).toISOString()).n}명`);

const levels = [0, 0, 0, 0, 0];
for (const c of all('SELECT affection FROM character WHERE ended_at IS NULL')) levels[levelOf(c.affection).level - 1]++;
console.log(`[캐릭터] 키우는 중 ${levels.reduce((a, b) => a + b, 0)}마리 (${levels.map((n, i) => `Lv${i + 1} ${n}`).join(' · ')}) · 완성 ${one('SELECT count(*) n FROM character WHERE affection >= ?', MAX_AFFECTION).n}마리`);

const picked = (date) => one('SELECT count(*) n FROM emotion_log WHERE date = ?', date).n;
const spread = all('SELECT emotion, count(*) n FROM emotion_log WHERE date = ? GROUP BY emotion', yesterday);
console.log(`[오늘의 감정] 오늘 ${picked(today)}명 · 어제 ${picked(yesterday)}명 · 최근 7일 동안 고른 사람 ${one('SELECT count(DISTINCT player_id) n FROM emotion_log WHERE date >= ?', weekAgo).n}명`);
console.log(`  어제 분포: ${EMOTIONS.map((e) => `${e} ${spread.find((r) => r.emotion === e)?.n ?? 0}`).join(' · ')}`);

const game = one('SELECT count(*) n, MAX(best_score) top FROM score WHERE month = ?', month);
console.log(`[게임 ${month}] 참여 ${game.n}명 · 최고 ${game.top ?? 0}점`);

const posts = (kind) => `오늘 ${one('SELECT count(*) n FROM post WHERE kind = ? AND date = ?', kind, today).n} · 전체 ${one('SELECT count(*) n FROM post WHERE kind = ?', kind).n}`;
console.log(`[글] 꽃밭 ${posts('garden')} / 소식 ${posts('news')}`);
console.log(`[묘약] 받은 묘약 ${one('SELECT count(*) n FROM potion').n}개`);
console.log(`[소식 작성권] ${all('SELECT nickname FROM player WHERE can_post_news = 1').map((r) => r.nickname).join(', ') || '없음'}`);
console.log(`[DB] ${(statSync(FILE).size / 1024).toFixed(0)}KB · 로그(event) ${one('SELECT count(*) n FROM event').n}줄`);
