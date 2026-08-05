const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const source = fs.readFileSync(
  'src/HuahaiClipboard.App/Assets/Web/host-scale.js',
  'utf8'
);
const context = { window: {} };
vm.runInNewContext(source, context);
const policy = context.window.HuahaiHostScale;

test('native host scale cancels WebView device pixel scaling', () => {
  assert.equal(policy.zoomForDevicePixelRatio(1), 1);
  assert.ok(Math.abs(policy.zoomForDevicePixelRatio(1.5) - (2 / 3)) < 0.0001);
  assert.equal(policy.zoomForDevicePixelRatio(2), 0.5);
  assert.equal(policy.zoomForDevicePixelRatio(0), 1);
});

test('native host layout restores the approved CSS viewport', () => {
  assert.equal(policy.layoutPixels(287, 2 / 3), 431);
  assert.equal(policy.layoutPixels(547, 2 / 3), 821);
  assert.equal(policy.layoutPixels(430, 1), 430);
});

test('panel scale is clamped to the approved proportional range', () => {
  assert.equal(policy.clampPanelScale(0.6), 0.8);
  assert.equal(policy.clampPanelScale(0.8), 0.8);
  assert.equal(policy.clampPanelScale(1.25), 1.25);
  assert.equal(policy.clampPanelScale(1.6), 1.6);
  assert.equal(policy.clampPanelScale(2), 1.6);
  assert.equal(policy.clampPanelScale('invalid'), 1);
});

test('only the Huahai virtual host is treated as the native desktop shell', () => {
  const webview = { postMessage() {} };

  assert.equal(policy.isNativeShellHost({ hostname: 'app.huahai.local' }, { webview }), true);
  assert.equal(policy.isNativeShellHost({ hostname: '127.0.0.1' }, { webview }), false);
  assert.equal(policy.isNativeShellHost({ hostname: 'localhost' }, { webview }), false);
  assert.equal(policy.isNativeShellHost({ hostname: 'app.huahai.local' }, {}), false);
});
