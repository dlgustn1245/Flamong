// 접속 주소의 QR 코드를 PNG 로 만든다. 의존성 없음(node 만).
// 사용: node deploy/make-qr.mjs [주소] [출력.png]     기본: https://flamong.lhsdev.info → deploy/flamong-qr.png
// QR 버전 3(29×29) · 오류 정정 M · 바이트 모드. 42바이트까지 담는다(주소가 더 길면 멈춘다)
import { writeFileSync } from 'node:fs';
import { deflateSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';

const text = process.argv[2] ?? 'https://flamong.lhsdev.info';
const out = process.argv[3] ?? fileURLToPath(new URL('./flamong-qr.png', import.meta.url));
const SIZE = 29;          // 버전 3
const DATA_CODEWORDS = 44; // 버전 3 · M
const EC_CODEWORDS = 26;
const SCALE = 24;         // 모듈 하나의 픽셀
const QUIET = 4;          // 둘레 여백(모듈)

// ── 데이터 코드워드: 모드(0100) + 길이(8비트) + 바이트 + 끝 표시 + 채움
const bytes = [...Buffer.from(text, 'utf8')];
if (bytes.length > 42) throw new Error(`주소가 너무 깁니다(${bytes.length}바이트, 42바이트까지)`);
const bits = [];
const push = (value, length) => { for (let i = length - 1; i >= 0; i--) bits.push((value >>> i) & 1); };
push(0b0100, 4);
push(bytes.length, 8);
for (const b of bytes) push(b, 8);
push(0, Math.min(4, DATA_CODEWORDS * 8 - bits.length));
while (bits.length % 8 !== 0) bits.push(0);
const data = [];
for (let i = 0; i < bits.length; i += 8) data.push(parseInt(bits.slice(i, i + 8).join(''), 2));
for (let pad = 0xec; data.length < DATA_CODEWORDS; pad ^= 0xec ^ 0x11) data.push(pad);

// ── 리드 솔로몬 오류 정정 (GF(256), 원시 다항식 0x11D)
const mul = (x, y) => {
  let z = 0;
  for (let i = 7; i >= 0; i--) {
    z = (z << 1) ^ ((z >>> 7) * 0x11d);
    z ^= ((y >>> i) & 1) * x;
  }
  return z;
};
const divisor = new Array(EC_CODEWORDS).fill(0);
divisor[EC_CODEWORDS - 1] = 1;
for (let i = 0, root = 1; i < EC_CODEWORDS; i++, root = mul(root, 2)) {
  for (let j = 0; j < EC_CODEWORDS; j++) {
    divisor[j] = mul(divisor[j], root);
    if (j + 1 < EC_CODEWORDS) divisor[j] ^= divisor[j + 1];
  }
}
const ec = new Array(EC_CODEWORDS).fill(0);
for (const b of data) {
  const factor = b ^ ec.shift();
  ec.push(0);
  divisor.forEach((coef, i) => { ec[i] ^= mul(coef, factor); });
}
const codewords = [...data, ...ec];

// ── 기능 무늬 (위치 찾기 · 타이밍 · 정렬 · 형식 정보 자리)
const modules = Array.from({ length: SIZE }, () => new Array(SIZE).fill(false));
const isFunction = Array.from({ length: SIZE }, () => new Array(SIZE).fill(false));
const setFunction = (x, y, dark) => { modules[y][x] = dark; isFunction[y][x] = true; };
for (let i = 0; i < SIZE; i++) {
  setFunction(6, i, i % 2 === 0);
  setFunction(i, 6, i % 2 === 0);
}
for (const [cx, cy] of [[3, 3], [SIZE - 4, 3], [3, SIZE - 4]]) {
  for (let dy = -4; dy <= 4; dy++) {
    for (let dx = -4; dx <= 4; dx++) {
      const x = cx + dx, y = cy + dy, dist = Math.max(Math.abs(dx), Math.abs(dy));
      if (x >= 0 && x < SIZE && y >= 0 && y < SIZE) setFunction(x, y, dist !== 2 && dist !== 4);
    }
  }
}
for (let dy = -2; dy <= 2; dy++) {
  for (let dx = -2; dx <= 2; dx++) setFunction(22 + dx, 22 + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1);
}
const drawFormat = (mask) => {
  const value = (0b00 << 3) | mask;   // 오류 정정 M = 00
  let rem = value;
  for (let i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >>> 9) * 0x537);
  const format = ((value << 10) | rem) ^ 0x5412;
  const bit = (i) => ((format >>> i) & 1) !== 0;
  for (let i = 0; i <= 5; i++) setFunction(8, i, bit(i));
  setFunction(8, 7, bit(6));
  setFunction(8, 8, bit(7));
  setFunction(7, 8, bit(8));
  for (let i = 9; i < 15; i++) setFunction(14 - i, 8, bit(i));
  for (let i = 0; i < 8; i++) setFunction(SIZE - 1 - i, 8, bit(i));
  for (let i = 8; i < 15; i++) setFunction(8, SIZE - 15 + i, bit(i));
  setFunction(8, SIZE - 8, true);
};
drawFormat(0);   // 자리만 먼저 잡는다

// ── 데이터를 오른쪽 아래부터 지그재그로 놓는다
let k = 0;
for (let right = SIZE - 1; right >= 1; right -= 2) {
  if (right === 6) right = 5;
  for (let vert = 0; vert < SIZE; vert++) {
    for (let j = 0; j < 2; j++) {
      const x = right - j;
      const y = ((right + 1) & 2) === 0 ? SIZE - 1 - vert : vert;
      if (!isFunction[y][x] && k < codewords.length * 8) {
        modules[y][x] = ((codewords[k >>> 3] >>> (7 - (k & 7))) & 1) !== 0;
        k++;
      }
    }
  }
}

// ── 마스크 8가지 중 벌점이 가장 낮은 것을 고른다
const MASKS = [
  (x, y) => (x + y) % 2 === 0,
  (x, y) => y % 2 === 0,
  (x, y) => x % 3 === 0,
  (x, y) => (x + y) % 3 === 0,
  (x, y) => (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0,
  (x, y) => ((x * y) % 2) + ((x * y) % 3) === 0,
  (x, y) => (((x * y) % 2) + ((x * y) % 3)) % 2 === 0,
  (x, y) => (((x + y) % 2) + ((x * y) % 3)) % 2 === 0,
];
const applyMask = (mask) => {
  for (let y = 0; y < SIZE; y++) {
    for (let x = 0; x < SIZE; x++) {
      if (!isFunction[y][x] && MASKS[mask](x, y)) modules[y][x] = !modules[y][x];
    }
  }
};
const penalty = () => {
  let score = 0, dark = 0;
  for (let a = 0; a < SIZE; a++) {
    for (const line of [modules[a], modules.map((row) => row[a])]) {
      let run = 1;
      for (let b = 1; b <= SIZE; b++) {
        if (b < SIZE && line[b] === line[b - 1]) { run++; continue; }
        if (run >= 5) score += run - 2;   // 같은 색이 5칸 이상 이어짐
        run = 1;
      }
    }
  }
  for (let y = 0; y < SIZE; y++) {
    for (let x = 0; x < SIZE; x++) {
      if (modules[y][x]) dark++;
      if (x + 1 < SIZE && y + 1 < SIZE && modules[y][x] === modules[y][x + 1] && modules[y][x] === modules[y + 1][x] && modules[y][x] === modules[y + 1][x + 1]) score += 3;
    }
  }
  return score + Math.floor(Math.abs(dark * 20 - SIZE * SIZE * 10) / (SIZE * SIZE)) * 10;   // 검은 칸이 절반에서 멀수록
};
let best = 0, bestScore = Infinity;
for (let mask = 0; mask < 8; mask++) {
  applyMask(mask);
  drawFormat(mask);
  const score = penalty();
  if (score < bestScore) { bestScore = score; best = mask; }
  applyMask(mask);   // 같은 마스크를 한 번 더 씌우면 원래대로
}
applyMask(best);
drawFormat(best);

// ── PNG (8비트 회색, 압축만 zlib)
const side = (SIZE + QUIET * 2) * SCALE;
const raw = Buffer.alloc((side + 1) * side, 255);
for (let py = 0; py < side; py++) {
  raw[py * (side + 1)] = 0;   // 줄마다 필터 없음
  const y = Math.floor(py / SCALE) - QUIET;
  for (let px = 0; px < side; px++) {
    const x = Math.floor(px / SCALE) - QUIET;
    if (x >= 0 && x < SIZE && y >= 0 && y < SIZE && modules[y][x]) raw[py * (side + 1) + 1 + px] = 0;
  }
}
const crcTable = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let i = 0; i < 8; i++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
const crc = (buf) => {
  let c = 0xffffffff;
  for (const b of buf) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
};
const chunk = (type, body) => {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(body.length, 0);
  head.write(type, 4, 'ascii');
  const tail = Buffer.alloc(4);
  tail.writeUInt32BE(crc(Buffer.concat([head.subarray(4), body])), 0);
  return Buffer.concat([head, body, tail]);
};
const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(side, 0);
ihdr.writeUInt32BE(side, 4);
ihdr[8] = 8;   // 비트 깊이
ihdr[9] = 0;   // 회색
writeFileSync(out, Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
  chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0)),
]));
console.log(`${text} → ${out} (${side}×${side}px, 마스크 ${best})`);
if (process.env.QR_DUMP) console.log(JSON.stringify(modules.map((row) => row.map((m) => (m ? 1 : 0)).join(''))));
