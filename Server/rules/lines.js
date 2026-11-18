import { createHash } from 'node:crypto';
import { SPECIES_PER_EGG } from './species.js';

// 플라몽 한마디: Main 말풍선에 뜨는 대사 표와 고르는 규칙. 순수 함수 (DB · HTTP 모름)
// 대사가 서버에만 있어 웹 빌드 없이 고치고 늘린다.
// 한 대사는 한두 줄(\n 으로 나눔), 줄마다 ROW_MAX 자 이하 · 폰트가 그리는 글자만 — test/lines.test.js 가 막는다.
// 공감하는 말만 쓴다. 직장 농담(야근 · 상사)과 훈계(「힘내야지!」)는 넣지 않는다
export const ROW_MAX = 13;

const WEEKEND = [
  '쉬는 날에도\n와 줬네!',
  '오늘은 푹 쉬어.\n나도 뒹굴뒹굴',
];

export const LINES = {
  // 요일. 인덱스는 Date#getUTCDay (0 = 일요일)
  weekday: [
    WEEKEND,
    ['주말 잘 보냈어?\n오늘은 천천히 가자', '다시 만나서 반가워!\n보고 싶었어'],
    ['화요일이다.\n어제보다 조금 가볍게', '오늘은 어떤 하루일까?\n두근두근'],
    ['벌써 한 주의\n반이나 왔어', '수요일엔\n하트가 더 달콤해'],
    ['내일만 지나면\n금요일이야', '목요일엔 내가 더\n귀여워지는 것 같아'],
    ['이번 주도\n잘 버텼다!', '주말에 뭐 할 거야?\n나도 알려 줘'],
    WEEKEND,
  ],
  // 레벨 1~5. 진화 그림이 바뀌는 곳(껍데기 → 손 → 발 → 귀 → 왕관)에 맞춘 말
  level: [
    ['아직 껍데기 속이 편해', '세상이 다\n처음 보는 거야'],
    ['손이 생겼어!\n하트 잡을 수 있어', '쑥쑥 크는 중이야.\n느껴져?'],
    ['이제 걸을 수 있어.\n따라갈래', '몸이 조금 무거워졌어.\n행복해서 그래'],
    ['귀가 생겨서\n네 말이 더 잘 들려', '나 좀\n멋있어진 것 같지?'],
    ['거의 다 컸어.\n조금만 더!', '반짝반짝,\n이게 다 네 덕분이야'],
  ],
  // 알 계열. 인덱스는 eggId (README 「캐릭터」 표 순서: 핑크 꽃 · 퍼플 밤하늘 · 블루 바다 · 그린 숲 · 그레이 구름 · 오렌지 햇살 · 라벤더 나비 · 크림 디저트)
  egg: [
    '오늘은 꽃이\n조금 더 피었어',
    '어젯밤 별 세다가\n잠들었어',
    '물방울 하나\n선물할게. 퐁!',
    '잎사귀가\n햇빛을 좋아해',
    '구름처럼\n둥실둥실한 기분이야',
    '내가 따뜻하게\n데워 줄게',
    '오늘은 팔랑팔랑\n날고 싶어',
    '달콤한 냄새 나지?\n나야',
  ],
  common: [
    '와 줘서 고마워',
    '오늘 하트\n기다리고 있었어',
    '점심 맛있는 거 먹어.\n나는 하트 먹을게',
    '네 얼굴 보니까\n배가 불러',
    '너랑 있으면\n몽글몽글해',
    '나 오늘 조금\n자란 것 같아',
    '꼬르륵,\n마음 한 입만 줄래?',
    '물 한 잔 마셨어?\n나는 하트 마실게',
  ],
  // 완성 뒤에는 레벨 · 알 계열 대신 이 줄
  completed: [
    '다 컸어! 도감에\n내 자리가 생겼어',
    '고마워. 네 마음 먹고\n이만큼 컸어',
    '다 자라도\n너랑 노는 건 좋아',
    '새 친구 데려와도 돼.\n나는 도감에서 기다릴게',
  ],
  // 오늘의 감정 맞장구. 감정마다 「공감 · 먹는 맛 · 함께」 3줄로 수와 톤을 맞춘다.
  // 어떤 감정의 말만 귀여우면 그 말을 보려고 실제와 다른 감정을 고르게 된다
  reply: {
    tired: ['많이 피곤했구나.\n천천히 가도 돼', '피곤한 마음은\n내가 냠냠 먹을게', '오늘은 쉬엄쉬엄.\n나도 같이 쉴게'],
    stress: ['속상한 일 있었구나.\n옆에 있을게', '그 마음 나한테 줘.\n꼭꼭 씹어 먹을게', '오늘 많이 애썼어.\n숨 한 번 크게 쉬자'],
    achievement: ['우와, 해냈구나!\n나까지 뿌듯해', '반짝반짝한 마음은\n톡톡 튀는 맛이야', '잘했어!\n이 기분 오래 기억하자'],
    focus: ['푹 빠져 있었구나.\n멋지다', '집중한 마음은\n고소한 맛이 나', '방해 안 할게.\n조용히 응원할게'],
    calm: ['평온한 하루구나.\n나도 포근해', '잔잔한 마음은\n달콤한 맛이야', '이런 날 좋다.\n오래오래 이랬으면'],
  },
};

/** seed 가 같으면 늘 같은 것을 고른다 (소식 담당 뽑기와 같은 방식) @param {string[]} list @param {string} seed */
function pick(list, seed) {
  return list[createHash('sha256').update(seed).digest().readUInt32BE(0) % list.length];
}

/**
 * 그날의 3줄: 요일 → 레벨 또는 알 계열(완성 뒤에는 완성 대사) → 공통.
 * 같은 날 같은 사람에게는 늘 같은 줄이 나온다. 레벨이 오르면 둘째 줄은 다음 /me 부터 바뀐다
 * @param {string} today KST 'YYYY-MM-DD'
 * @param {string} playerId
 * @param {number} speciesId
 * @param {{ level: number, completed: boolean }} growth levelOf · growthOf 결과
 * @returns {string[]}
 */
export function linesOf(today, playerId, speciesId, { level, completed }) {
  const seed = `${today}:${playerId}`;
  const second = completed
    ? LINES.completed
    : [...LINES.level[level - 1], LINES.egg[Math.floor(speciesId / SPECIES_PER_EGG)]];
  return [
    pick(LINES.weekday[new Date(today).getUTCDay()], `${seed}:day`),   // 날짜만 있는 문자열은 UTC 0시로 읽힌다
    pick(second, `${seed}:second`),
    pick(LINES.common, `${seed}:common`),
  ];
}

/** 오늘의 감정을 고른 직후의 맞장구 한 줄 @param {string} today @param {string} playerId @param {string} emotion growth.js EMOTIONS 중 하나 */
export function replyOf(today, playerId, emotion) {
  return pick(LINES.reply[emotion], `${today}:${playerId}:reply`);
}
