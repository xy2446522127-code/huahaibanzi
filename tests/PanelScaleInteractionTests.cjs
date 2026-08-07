const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const source = fs.readFileSync(
  'src/HuahaiClipboard.App/Assets/Web/panel-scale.js',
  'utf8'
);
const context = { window: {} };
vm.runInNewContext(source, context);
const scale = context.window.HuahaiPanelScale;

test('scale accepts every one-percent value from 80 through 160', () => {
  for (let percent = 80; percent <= 160; percent += 1) {
    assert.equal(scale.normalizePercent(percent), percent);
    assert.equal(scale.toRatio(percent), percent / 100);
  }
  assert.equal(scale.normalizePercent(79), 80);
  assert.equal(scale.normalizePercent(161), 160);
  assert.equal(scale.normalizePercent('invalid'), 100);
});

test('rapid preview is frame-coalesced while commit runs exactly once', () => {
  const frames = new Map();
  const rendered = [];
  const previews = [];
  const commits = [];
  let nextFrame = 1;
  const controller = scale.createController({
    scheduleFrame(callback) {
      const id = nextFrame++;
      frames.set(id, callback);
      return id;
    },
    cancelFrame(id) { frames.delete(id); },
    render(percent) { rendered.push(percent); },
    preview(ratio) { previews.push(ratio); },
    commit(ratio) { commits.push(ratio); },
  });

  controller.setCommitted(100);
  controller.preview(81);
  controller.preview(149);
  assert.deepEqual(previews, []);
  assert.equal(frames.size, 1);

  const callback = [...frames.values()][0];
  frames.clear();
  callback();
  assert.deepEqual(previews, [1.49]);
  assert.equal(rendered.at(-1), 149);
  assert.deepEqual(commits, []);

  controller.preview(159);
  controller.commit(117);
  assert.equal(frames.size, 0);
  assert.equal(rendered.at(-1), 117);
  assert.deepEqual(commits, [1.17]);
});

test('cancel restores the last committed scale without persisting a preview', () => {
  const rendered = [];
  const previews = [];
  const commits = [];
  let pending;
  const controller = scale.createController({
    scheduleFrame(callback) { pending = callback; return 1; },
    cancelFrame() { pending = undefined; },
    render(percent) { rendered.push(percent); },
    preview(ratio) { previews.push(ratio); },
    commit(ratio) { commits.push(ratio); },
  });

  controller.setCommitted(83);
  controller.preview(159);
  pending();
  controller.cancel();

  assert.equal(rendered.at(-1), 83);
  assert.deepEqual(commits, []);
  assert.deepEqual(previews, [1.59, 0.83]);
});
