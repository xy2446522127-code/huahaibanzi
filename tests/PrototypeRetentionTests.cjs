const test = require('node:test');
const assert = require('node:assert/strict');
const retention = require('../.superpowers/brainstorm/visual-companion-2/content/retention-policy.js');

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
