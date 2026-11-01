// 닉네임 · 글에 쓸 수 있는 글자 = 클라 폰트가 그릴 수 있는 범위.
// 꾸불림체 정적 아틀라스(ASCII · 완성형 2350자) + 자모 폴백(ㄱ~ㅣ 51자) + 동적 폴백(한글 11172자 전부).
// 이모지 · 전각 문자 · 줄바꿈은 □ 로 보이거나 카드를 깨서 받지 않는다
const ALLOWED = /^[\x20-\x7eㄱ-ㅣ가-힣]+$/;

export const NICKNAME_MAX = 12;
export const POST_MAX = 100;

/** 허용 글자만 있고 1~max 자인지. 허용 글자는 모두 BMP 라 length 가 글자 수다 @param {string} s @param {number} max */
export function isValidText(s, max) {
  return typeof s === 'string' && s.length <= max && ALLOWED.test(s);
}
