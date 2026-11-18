// 운영 서버에서 도는 스크립트. reset-password.sh 가 맨 앞에 ARGS · MODE 두 줄을 붙여 ssh 로 넘긴다
//   ARGS: base64("닉네임\n새 비밀번호")   MODE: check(찾기만) | dry(바꿔 보고 되돌림) | apply(바꿈)
// 비밀번호를 명령줄에 싣지 않으려고 스크립트 본문에 넣어 보낸다(서버의 프로세스 목록에 남지 않는다)
import { DatabaseSync } from 'node:sqlite';
import { hashPassword, PASSWORD_PATTERN, verifyPassword } from '/opt/flamong/server/rules/password.js';

const [nick, pw] = Buffer.from(ARGS, 'base64').toString('utf8').split('\n');
const db = new DatabaseSync('/var/lib/flamong/flamong.db', { timeout: 5000 });
const find = db.prepare('SELECT nickname, created_at, locked_until, password_hash FROM player WHERE nickname = ? COLLATE NOCASE');

const p = find.get(nick);
if (!p) {
  console.log(`[없음] 「${nick}」 닉네임이 없습니다. 대소문자는 가리지 않지만 띄어쓰기는 같아야 합니다.`);
  process.exit(3);
}
const joined = new Date(new Date(p.created_at).getTime() + 9 * 3_600_000).toISOString().slice(0, 10);
if (MODE === 'check') {
  console.log(`찾았습니다: ${p.nickname} (가입 ${joined})${p.locked_until ? ' · 로그인 잠금 중' : ''}`);
  process.exit(0);
}

// 가입과 같은 규칙이 아니면 바꾸지 않는다
if (!new RegExp(PASSWORD_PATTERN).test(pw)) {
  console.log('[중단] 비밀번호는 숫자 4~6자리여야 합니다.');
  process.exit(2);
}
db.exec('BEGIN');
const changes = db.prepare('UPDATE player SET password_hash = ?, login_fails = 0, locked_until = NULL WHERE nickname = ? COLLATE NOCASE')
  .run(hashPassword(pw), nick).changes;
const ok = changes === 1 && verifyPassword(pw, find.get(nick).password_hash);
if (MODE === 'apply' && ok) {
  db.exec('COMMIT');
  console.log(`바꿨습니다: ${p.nickname} 의 비밀번호를 새로 정하고 로그인 잠금을 풀었습니다.`);
} else {
  db.exec('ROLLBACK');
  console.log(ok ? `[시험] ${p.nickname}: 바꿀 수 있습니다(되돌렸습니다).` : '[실패] 바꾸지 못했습니다. 아무것도 바뀌지 않았습니다.');
  process.exit(ok ? 0 : 1);
}
