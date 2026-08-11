const test = require('node:test');
const assert = require('node:assert/strict');
const retention = require('../src/HuahaiClipboard.App/Assets/Web/retention-policy.js');
const fs = require('node:fs');
const shell = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

const records = [
  { id: 1, ageDays: 4, fav: false, pin: false },
  { id: 2, ageDays: 40, fav: true, pin: false },
  { id: 3, ageDays: 40, fav: false, pin: true },
  { id: 4, ageDays: 1, fav: false, pin: false }
];

test('automatic cleanup accepts only the three supported retention periods', () => {
  assert.equal(retention.normalizeDays(3), 3);
  assert.equal(retention.normalizeDays('30'), 30);
  assert.equal(retention.normalizeDays(14), 7);
});

test('automatic cleanup removes expired ordinary records but keeps protected records', () => {
  assert.deepEqual(
    retention.prune(records, 3).map(record => record.id),
    [2, 3, 4]
  );
});

test('clearing ordinary history preserves favorite and pinned records', () => {
  assert.deepEqual(
    retention.clearOrdinary(records).map(record => record.id),
    [2, 3]
  );
});

test('clearing everything removes favorite and pinned records too', () => {
  assert.deepEqual(retention.clearEverything(records), []);
});

test('count cleanup accepts 1 through 10000 and rejects other values', () => {
  assert.equal(retention.normalizeCountLimit(1), 1);
  assert.equal(retention.normalizeCountLimit('10000'), 10000);
  assert.equal(retention.normalizeCountLimit(0), null);
  assert.equal(retention.normalizeCountLimit(10001), null);
  assert.equal(retention.normalizeCountLimit(3.5), null);
});

test('count cleanup removes the oldest ordinary records and keeps protected records', () => {
  const values = [
    { id: 1, copiedAt: 1, fav: false, pin: false },
    { id: 2, copiedAt: 2, fav: true, pin: false },
    { id: 3, copiedAt: 3, fav: false, pin: false },
    { id: 4, copiedAt: 4, fav: false, pin: true },
    { id: 5, copiedAt: 5, fav: false, pin: false }
  ];

  assert.deepEqual(retention.trimOrdinary(values, 2).map(record => record.id), [2, 3, 4, 5]);
});

test('formal storage settings expose persisted count cleanup with the existing glass progress style', () => {
  for (const id of ['autoCleanupCountToggle', 'autoCleanupCountInput', 'autoCleanupCountText', 'autoCleanupCountProgress']) {
    assert.match(shell, new RegExp(`id="${id}"`), id);
  }
  assert.match(shell, /postNative\('setAutoCleanupCountEnabled',\{enabled\}\)/);
  assert.match(shell, /postNative\('setAutoCleanupCount',\{number:limit\}\)/);
  assert.match(shell, /class="retention-progress"/);
});
