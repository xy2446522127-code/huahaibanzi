const test = require('node:test');
const assert = require('node:assert/strict');

const dragPolicy = require('../src/HuahaiClipboard.App/Assets/Web/panel-drag.js');
const fs = require('node:fs');
const shell = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');
const windowHost = fs.readFileSync('src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs', 'utf8');

test('left press on a non-interactive surface begins dragging immediately', () => {
  assert.equal(dragPolicy.holdDurationMs, 0);
  assert.equal(dragPolicy.shouldBegin({ button: 0, elapsedMs: 0, distancePx: 0, interactive: false }), true);
  assert.equal(dragPolicy.shouldBegin({ button: 1, elapsedMs: 100, distancePx: 0, interactive: false }), false);
  assert.equal(dragPolicy.shouldBegin({ button: 0, elapsedMs: 100, distancePx: 0, interactive: true }), false);
});

test('records controls inputs and scrolling regions never arm panel dragging', () => {
  const blocked = ['button', 'input', 'textarea', 'label', '.record', '.record-list', '.filters', '.settings-nav'];

  for (const selector of blocked) {
    assert.equal(dragPolicy.isInteractiveTarget({ closest: value => value.includes(selector) }), true, selector);
  }
  assert.equal(dragPolicy.isInteractiveTarget({ closest: () => null }), false);
});

test('preview drag position is clamped inside the desktop surface', () => {
  assert.deepEqual(
    dragPolicy.previewPosition({ startLeft: 100, startTop: 80, deltaX: 25, deltaY: -20, panelWidth: 430, panelHeight: 680, surfaceWidth: 1180, surfaceHeight: 760 }),
    { left: 125, top: 60 }
  );
  assert.deepEqual(
    dragPolicy.previewPosition({ startLeft: 900, startTop: 400, deltaX: 100, deltaY: 100, panelWidth: 430, panelHeight: 680, surfaceWidth: 1180, surfaceHeight: 760 }),
    { left: 750, top: 80 }
  );
});

test('native panel dragging coalesces movement to display frames without per-frame WebView bridge traffic', () => {
  assert.match(shell, /postNative\('beginNativeDrag'\)/);
  assert.doesNotMatch(shell, /physicalScreenPoint/);
  assert.doesNotMatch(shell, /postNative\('beginSystemDrag'/);
  assert.doesNotMatch(shell, /postNative\('dragMove'/);
  assert.doesNotMatch(shell, /postNative\('endDrag'/);
  assert.match(windowHost, /CompositionTarget\.Rendering \+= DragCompositionTarget_Rendering/);
  assert.doesNotMatch(windowHost, /dragTimer\.Interval = TimeSpan\.FromMilliseconds\(8\)/);
  assert.match(windowHost, /GetAsyncKeyState\(LeftMouseButton\)/);
  assert.match(windowHost, /GetCursorPos\(/);
  assert.match(windowHost, /BeginNativeWindowDrag\(\)/);
  assert.match(windowHost, /appWindow\.Move\(/);
});
