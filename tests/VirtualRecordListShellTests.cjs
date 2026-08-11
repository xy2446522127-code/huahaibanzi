const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const shellPath = 'src/HuahaiClipboard.App/Assets/Web/product-shell.html';
const html = fs.readFileSync(shellPath, 'utf8');

test('formal shell loads and uses the virtual record module', () => {
  assert.match(html, /<script src="virtual-record-list\.js"><\/script>/);
  assert.match(html, /HuahaiVirtualRecordList\.calculateWindow/);
  assert.match(html, /HuahaiVirtualRecordList\.createFrameScheduler/);
});

test('record actions are delegated once from the list container', () => {
  assert.match(html, /recordList\.addEventListener\('click'/);
  assert.doesNotMatch(html, /hhQA\('\.record'\)\.forEach\(row=>/);
});

test('virtual records preserve scroll height with bounded spacers', () => {
  assert.match(html, /class="virtual-spacer top"/);
  assert.match(html, /class="virtual-spacer bottom"/);
  assert.match(html, /topSpacer/);
  assert.match(html, /bottomSpacer/);
  assert.match(html, /rowExtent/);
});

test('scroll rendering is frame-coalesced and passive', () => {
  assert.match(html, /virtualRenderScheduler\.request/);
  assert.match(html, /addEventListener\('scroll',[^;]+\{passive:true\}/);
});

test('filtering and searching still derive from the full in-memory item set', () => {
  assert.match(html, /function filteredItems\(\)/);
  assert.match(html, /const filtered=filteredItems\(\)/);
  assert.match(html, /filtered\.slice\(windowState\.start,windowState\.end\)/);
});
