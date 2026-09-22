// 닉네임 허용 문자 = 클라 폰트 아틀라스(CharSet_KSX1001.txt)와 같은 집합.
// 유니코드 한글 범위(AC00-D7A3)는 11172자라 아틀라스 밖 글자('뷁' 등)가 통과한다. 완성형 2350자를 직접 만든다.
const decoder = new TextDecoder('euc-kr');
const KSX1001 = new Set();
for (let hi = 0xb0; hi <= 0xc8; hi++) {
  for (let lo = 0xa1; lo <= 0xfe; lo++) {
    KSX1001.add(decoder.decode(Uint8Array.of(hi, lo)));
  }
}

export const NICKNAME_MAX = 12;

/** @param {string} s */
export function isValidNickname(s) {
  if (typeof s !== 'string' || s.length === 0 || s.length > NICKNAME_MAX) return false;
  for (const ch of s) {
    const code = ch.codePointAt(0);
    const ascii = code >= 0x20 && code <= 0x7e;
    if (!ascii && !KSX1001.has(ch)) return false;
  }
  return true;
}
