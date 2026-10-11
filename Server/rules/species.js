// 알(eggId 0~7, README 「캐릭터」 표 순서)마다 캐릭터 SPECIES_PER_EGG 종.
// speciesId = eggId × SPECIES_PER_EGG + (0 ~ SPECIES_PER_EGG-1)
// ponytail: 알당 3종은 임시값 (README 「미정」 캐릭터 수). 정해지면 이 숫자만 고친다
export const EGG_COUNT = 8;
export const SPECIES_PER_EGG = 3;

/**
 * 알 계열 안에서 균등 랜덤. 클라이언트에서 뽑지 않는다 (새로고침 리롤 방지)
 * @param {number} eggId @param {() => number} random 테스트에서 고정값 주입
 */
export function pickSpecies(eggId, random = Math.random) {
  return eggId * SPECIES_PER_EGG + Math.floor(random() * SPECIES_PER_EGG);
}
