const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const files = [
  '.superpowers/brainstorm/visual-companion-2/content/interactive-product-preview-v6.html',
  'src/HuahaiClipboard.App/Assets/Web/product-shell.html'
];

function fontSize(css, selector) {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = css.match(new RegExp(`${escaped}\\{[^}]*font-size:(\\d+(?:\\.\\d+)?)px`));
  assert.ok(match, `Missing font size for ${selector}`);
  return Number(match[1]);
}

for (const file of files) {
  test(`${file} uses the approved readable typography floor`, () => {
    const html = fs.readFileSync(file, 'utf8');

    assert.ok(fontSize(html, '.record-text') >= 13, 'Clipboard primary text must be at least 13px.');
    assert.ok(fontSize(html, '.record-text small') >= 10, 'Clipboard metadata must be at least 10px.');
    assert.ok(fontSize(html, '.setting-row') >= 12, 'Settings labels must be at least 12px.');
    assert.ok(fontSize(html, '.setting-row small') >= 10, 'Settings descriptions must be at least 10px.');
    assert.ok(fontSize(html, '.capture') >= 11, 'Shortcut recorder text must be at least 11px.');
  });
}
