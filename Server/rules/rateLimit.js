// IP당 요청 횟수 제한. 고정된 시간 창마다 센다. 시각은 밖에서 받는다 (규칙 코드에서 new Date() 금지)
// ponytail: 메모리 Map 하나. 서버를 다시 켜면 초기화되고, 서버가 여러 대가 되면 공유되지 않는다. 그때 DB 나 Redis 로

/** @typedef {{ count: number, resetAt: number }} Hit */

/**
 * 이번 요청을 세고, 창 안에서 max 를 넘으면 true(막음)
 * @param {Map<string, Hit>} hits @param {string} key @param {number} max @param {number} windowMs @param {number} nowMs
 */
export function isLimited(hits, key, max, windowMs, nowMs) {
  const hit = hits.get(key);
  if (!hit || nowMs >= hit.resetAt) {
    hits.set(key, { count: 1, resetAt: nowMs + windowMs });
    return false;
  }
  hit.count++;
  return hit.count > max;
}

/** 창이 지난 기록을 지운다. Map 이 끝없이 커지지 않게 @param {Map<string, Hit>} hits @param {number} nowMs */
export function pruneHits(hits, nowMs) {
  for (const [key, hit] of hits) {
    if (nowMs >= hit.resetAt) hits.delete(key);
  }
}
