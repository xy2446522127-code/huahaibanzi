const test = require('node:test');
const assert = require('node:assert/strict');

const virtualList = require('../src/HuahaiClipboard.App/Assets/Web/virtual-record-list.js');

test('1,000 records render only a bounded visible window', () => {
  const result = virtualList.calculateWindow({
    itemCount: 1000,
    scrollTop: 35000,
    viewportHeight: 520,
    rowExtent: 76,
    overscan: 4,
  });

  assert.ok(result.end - result.start <= 16);
  assert.equal(result.topSpacer, result.start * 76);
  assert.equal(result.bottomSpacer, (1000 - result.end) * 76);
});

test('the final scroll position includes the final record without overflow', () => {
  const result = virtualList.calculateWindow({
    itemCount: 1000,
    scrollTop: 76000,
    viewportHeight: 520,
    rowExtent: 76,
    overscan: 4,
  });

  assert.equal(result.end, 1000);
  assert.ok(result.start >= 980);
});

test('empty and invalid measurements produce a safe empty window', () => {
  assert.deepEqual(
    virtualList.calculateWindow({ itemCount: 0, scrollTop: 20, viewportHeight: 500, rowExtent: 76 }),
    { start: 0, end: 0, topSpacer: 0, bottomSpacer: 0 },
  );
  const result = virtualList.calculateWindow({
    itemCount: 3,
    scrollTop: -10,
    viewportHeight: 0,
    rowExtent: 0,
    overscan: -2,
  });
  assert.deepEqual(result, { start: 0, end: 1, topSpacer: 0, bottomSpacer: 2 });
});

test('frame scheduler renders only the latest request once per frame', () => {
  const callbacks = [];
  const rendered = [];
  const scheduler = virtualList.createFrameScheduler({
    scheduleFrame: callback => {
      callbacks.push(callback);
      return callbacks.length;
    },
    cancelFrame: () => {},
    render: value => rendered.push(value),
  });

  scheduler.request(10);
  scheduler.request(20);
  assert.deepEqual(rendered, []);
  callbacks.shift()();
  assert.deepEqual(rendered, [20]);
  scheduler.request(30);
  callbacks.shift()();
  assert.deepEqual(rendered, [20, 30]);
});
