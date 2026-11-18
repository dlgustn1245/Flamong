import { test } from 'node:test';
import assert from 'node:assert/strict';
import { EMOTIONS, levelOf } from '../rules/growth.js';
import { LINES, linesOf, replyOf, ROW_MAX } from '../rules/lines.js';
import { EGG_COUNT } from '../rules/species.js';
import { isValidText } from '../rules/text.js';

const ALL = [
  ...LINES.weekday.flat(), ...LINES.level.flat(), ...LINES.egg,
  ...LINES.common, ...LINES.completed, ...Object.values(LINES.reply).flat(),
];

test('대사: 한두 줄 · 줄마다 ROW_MAX 자 이하 · 폰트가 그리는 글자만', () => {
  for (const line of ALL) {
    const rows = line.split('\n');
    assert.ok(rows.length <= 2, line);
    for (const row of rows) assert.ok(isValidText(row, ROW_MAX), line);
  }
});

test('대사 표: 요일 7칸 · 레벨 5칸 · 알 8칸 · 감정마다 3줄', () => {
  assert.equal(LINES.weekday.length, 7);
  assert.equal(LINES.level.length, 5);
  assert.equal(LINES.egg.length, EGG_COUNT);
  assert.deepEqual(Object.keys(LINES.reply), EMOTIONS);
  for (const e of EMOTIONS) assert.equal(LINES.reply[e].length, 3, e);
});

test('linesOf: 요일 → 레벨 또는 알 계열 → 공통 · 같은 날은 같은 줄 · 날이 바뀌면 달라진다', () => {
  const lv3 = levelOf(40);
  const mon = linesOf('2026-11-02', 'p1', 7, lv3);   // 월요일 · 알 2(블루) · Lv3
  assert.equal(mon.length, 3);
  assert.ok(LINES.weekday[1].includes(mon[0]));
  assert.ok([...LINES.level[2], LINES.egg[2]].includes(mon[1]));
  assert.ok(LINES.common.includes(mon[2]));
  assert.deepEqual(linesOf('2026-11-02', 'p1', 7, lv3), mon);
  assert.ok(LINES.weekday[0].includes(linesOf('2026-11-01', 'p1', 7, lv3)[0]));   // 일요일

  const seen = new Set();
  for (let d = 1; d <= 30; d++) seen.add(linesOf(`2026-11-${String(d).padStart(2, '0')}`, 'p1', 7, lv3).join('|'));
  assert.ok(seen.size > 10, `30일 동안 ${seen.size}가지`);
});

test('linesOf: 완성 뒤에는 둘째 줄이 완성 대사', () => {
  assert.ok(LINES.completed.includes(linesOf('2026-11-02', 'p1', 7, levelOf(175))[1]));
});

test('replyOf: 고른 감정의 맞장구', () => {
  for (const e of EMOTIONS) assert.ok(LINES.reply[e].includes(replyOf('2026-11-02', 'p1', e)), e);
});
