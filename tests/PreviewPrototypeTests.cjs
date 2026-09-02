const test = require('node:test');
const assert = require('node:assert/strict');

const preview = require('../src/HuahaiClipboard.App/Assets/Web/preview-prototype.js');

test('editing a record creates a dirty draft without mutating the source record', () => {
  const record = { id: 'r1', kind: '文本', text: '原始内容', note: '' };
  const state = preview.createState(record);
  const edited = preview.updateDraft(state, '新的完整内容');

  assert.equal(record.text, '原始内容');
  assert.equal(edited.draft, '新的完整内容');
  assert.equal(edited.dirty, true);
  assert.equal(edited.visible, true);
});

test('saving content and a note updates only the selected record and hides the preview', () => {
  const records = [
    { id: 'r1', kind: '文本', text: '原始内容', note: '' },
    { id: 'r2', kind: '文本', text: '不要修改', note: '原备注' },
  ];
  let state = preview.createState(records[0]);
  state = preview.updateDraft(state, '已保存内容');
  state = preview.updateNoteDraft(state, '客户确认后发送');

  const result = preview.save(state, records);

  assert.equal(result.ok, true);
  assert.equal(result.state.visible, false);
  assert.equal(result.state.dirty, false);
  assert.deepEqual(result.records[0], {
    id: 'r1',
    kind: '文本',
    text: '已保存内容',
    note: '客户确认后发送',
  });
  assert.deepEqual(result.records[1], records[1]);
});

test('empty text cannot be saved and preserves the visible draft', () => {
  const records = [{ id: 'r1', kind: '文本', text: '原始内容', note: '' }];
  const state = preview.updateDraft(preview.createState(records[0]), '   ');

  const result = preview.save(state, records);

  assert.equal(result.ok, false);
  assert.equal(result.error, '内容不能为空');
  assert.equal(result.state.visible, true);
  assert.equal(result.records[0].text, '原始内容');
});

test('drag geometry is clamped inside the desktop viewport', () => {
  assert.deepEqual(
    preview.moveGeometry(
      { left: 100, top: 80, width: 650, height: 500 },
      { x: 900, y: 700 },
      { width: 1180, height: 760 },
    ),
    { left: 530, top: 260, width: 650, height: 500 },
  );
});

test('resize geometry honors paper minimum size and viewport bounds', () => {
  assert.deepEqual(
    preview.resizeGeometry(
      { left: 120, top: 70, width: 650, height: 500 },
      { x: -500, y: -500 },
      { width: 1180, height: 760 },
    ),
    { left: 120, top: 70, width: 420, height: 360 },
  );
  assert.deepEqual(
    preview.resizeGeometry(
      { left: 120, top: 70, width: 650, height: 500 },
      { x: 900, y: 900 },
      { width: 1180, height: 760 },
    ),
    { left: 120, top: 70, width: 1060, height: 690 },
  );
});

test('preview topmost is initialized from and writes back to the selected history record pin', () => {
  const records = [
    { id: 'r1', kind: '文本', text: '已置顶内容', pin: true },
    { id: 'r2', kind: '文本', text: '普通内容', pin: false },
  ];

  assert.equal(preview.createState(records[0]).topmost, true);
  assert.equal(preview.createState(records[1]).topmost, false);
  assert.deepEqual(preview.setRecordPin(records, 'r2', true), [
    records[0],
    { ...records[1], pin: true },
  ]);
});

test('embedded preview gives vertical space to the editor instead of inherited heading margins', () => {
  const fs = require('node:fs');
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /\.desktop > \.preview-window \.preview-header\{[^}]*padding:10px 14px 9px/);
  assert.match(html, /\.desktop > \.preview-window #previewTitle\{[^}]*margin:0[^}]*font-size:20px/);
  assert.match(html, /\.desktop > \.preview-window #previewMeta\{[^}]*margin:3px 0 0[^}]*font-size:12px/);
  assert.match(html, /\.desktop > \.preview-window \.preview-content\{[^}]*padding:10px 14px/);
  assert.match(html, /\.desktop > \.preview-window \.preview-footer\{[^}]*padding:8px 14px/);
});

test('todo checkmarks use an explicit centered glyph box', () => {
  const fs = require('node:fs');
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /\.todo-check-v2\{[^}]*padding:0[^}]*line-height:1/);
  assert.match(html, /\.todo-check-v2\.done::after\{[^}]*display:block[^}]*line-height:1[^}]*transform:translateY\(-\.5px\)/);
});
