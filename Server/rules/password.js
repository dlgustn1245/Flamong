import { randomBytes, scryptSync, timingSafeEqual } from 'node:crypto';

// 숫자 4~6자리. 가입 스키마 · 클라이언트 입력칸(숫자 전용, 최대 6자리)과 같게
export const PASSWORD_PATTERN = '^[0-9]{4,6}$';

// 4자리 숫자는 1만 가지뿐이라 마구 넣어 보면 금방 뚫린다. 계정마다 틀린 횟수로 잠근다
export const MAX_LOGIN_FAILS = 5;
export const LOCK_MS = 10 * 60_000;

// ponytail: scryptSync 는 한 번에 수십 ms 동안 이벤트 루프를 막는다. 가입 · 로그인에만 쓰니 사내 규모에선 괜찮고, 몰리면 비동기 scrypt 로
/** 저장 형식 "salt:hash" (hex). 같은 비밀번호도 salt 가 달라 매번 다른 값이 된다 @param {string} password */
export function hashPassword(password) {
  const salt = randomBytes(16);
  return `${salt.toString('hex')}:${scryptSync(password, salt, 32).toString('hex')}`;
}

/** @param {string} password @param {string} stored hashPassword 결과 */
export function verifyPassword(password, stored) {
  const [salt, hash] = stored.split(':');
  const expected = Buffer.from(hash, 'hex');
  return timingSafeEqual(scryptSync(password, Buffer.from(salt, 'hex'), expected.length), expected);
}

/** @param {Date} now @returns {string} 잠금이 풀리는 시각 (ISO) */
export function lockUntil(now) {
  return new Date(now.getTime() + LOCK_MS).toISOString();
}

/** @param {string | null} lockedUntil @param {Date} now */
export function isLocked(lockedUntil, now) {
  return lockedUntil != null && Date.parse(lockedUntil) > now.getTime();
}
