import { readFileSync } from 'node:fs';

// 닉네임 · 글에 쓸 수 있는 글자 = 클라 폰트가 그릴 수 있는 범위.
// 꾸불림체가 실제로 그리는 한글은 2,780자뿐이다(KS X 1001 2,350 + 430). 나머지 8,392자는 폰트에 자리만 있고 모양이 비어 공백처럼 보인다.
// hangul.txt 는 그 2,780자를 폰트에서 뽑은 목록. ASCII · 자모(ㄱ~ㅣ 51자)는 정규식으로 본다.
// 이모지 · 전각 문자 · 줄바꿈은 □ 로 보이거나 카드를 깨서 받지 않는다
const HANGUL = new Set(readFileSync(new URL('./hangul.txt', import.meta.url), 'utf8'));
const BASIC = /^[\x20-\x7eㄱ-ㅣ]$/;

export const NICKNAME_MAX = 12;
export const PROFILE_MAX = 20;   // 팀 · 이름
export const POST_MAX = 100;

/** 허용 글자만 있고 1~max 자인지. 허용 글자는 모두 BMP 라 length 가 글자 수다 @param {string} s @param {number} max */
export function isValidText(s, max) {
  if (typeof s !== 'string' || s.length === 0 || s.length > max) return false;
  for (const ch of s) {
    if (!BASIC.test(ch) && !HANGUL.has(ch)) return false;
  }
  return true;
}
