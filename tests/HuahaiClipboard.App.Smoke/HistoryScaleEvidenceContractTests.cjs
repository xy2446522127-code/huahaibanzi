const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const probe = fs.readFileSync('tests/HuahaiClipboard.App.Smoke/HistoryScalePerformanceProbe.cjs', 'utf8');
const wrapper = fs.readFileSync('tests/HuahaiClipboard.App.Smoke/HistoryScalePerformanceSmoke.ps1', 'utf8');
const clipboardSmoke = fs.readFileSync('tests/HuahaiClipboard.App.Smoke/ProductionClipboardSmoke.ps1', 'utf8');

test('history performance evidence binds the tested executable, source revision, and WebView2 runtime', () => {
  assert.match(wrapper, /HUAHAI_HISTORY_EXE_SHA256/);
  assert.match(wrapper, /HUAHAI_HISTORY_SOURCE_REVISION/);
  assert.match(probe, /Browser\.getVersion/);
  assert.match(probe, /report\.identity\s*=/);
  assert.match(probe, /executableSha256/);
  assert.match(probe, /sourceRevision/);
});

test('1,000-record runtime journey exercises search, filter, and delegated actions outside the initial window', () => {
  assert.match(probe, /journeys:/);
  assert.match(probe, /journeys:\s*\{\s*search,\s*filter,\s*delegatedActions\s*\}/);
  for (const action of ['copy', 'togglePin', 'toggleFavorite', 'delete']) {
    assert.match(probe, new RegExp(`['\"]${action}['\"]`));
  }
});

test('clipboard image clones remain alive until the final restore completes', () => {
  const finalFinally = clipboardSmoke.lastIndexOf('\nfinally {');
  assert.notEqual(finalFinally, -1);
  const beforeFinally = clipboardSmoke.slice(0, finalFinally);
  const finalCleanup = clipboardSmoke.slice(finalFinally);
  assert.doesNotMatch(beforeFinally, /clipboardDisposables\) \{ \$disposable\.Dispose\(\) \}/);
  assert.match(
    finalCleanup,
    /Restore-ClipboardSnapshot \$clipboardSnapshot \$clipboardWasEmpty[\s\S]*clipboardDisposables\) \{ \$disposable\.Dispose\(\) \}/,
  );
});
