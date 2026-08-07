const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const files = [
  '.superpowers/brainstorm/visual-companion-2/content/interactive-product-preview-v6.html',
  'src/HuahaiClipboard.App/Assets/Web/product-shell.html'
];

const count = (text, part) => text.split(part).length - 1;

for (const file of files) {
  test(`${file} declares UTF-8 before any visible or executable content`, () => {
    const firstBytes = fs.readFileSync(file).subarray(0, 256).toString('ascii').toLowerCase();

    assert.match(firstBytes, /^\s*<meta\s+charset=["']?utf-8["']?\s*>/);
  });

  test(`${file} exposes the complete retention controls`, () => {
    const html = fs.readFileSync(file, 'utf8');

    assert.equal(count(html, 'data-days="'), 3);
    assert.equal(count(html, 'id="clearOrdinaryHistory"'), 1);
    assert.equal(count(html, 'id="clearAllHistory"'), 1);
    assert.equal(count(html, 'src="retention-policy.js"'), 1);
  });

  test(`${file} contains valid executable module syntax`, () => {
    const html = fs.readFileSync(file, 'utf8');
    const marker = '<script type="module">';
    const start = html.indexOf(marker);
    const end = html.lastIndexOf('</script>');

    assert.notEqual(start, -1);
    assert.notEqual(end, -1);
    assert.doesNotThrow(() => new vm.Script(
      html.slice(start + marker.length, end),
      { filename: file }
    ));
  });

  test(`${file} resolves the fox icon from its packaged local assets`, () => {
    const html = fs.readFileSync(file, 'utf8');
    const match = html.match(/\.fox\{[^}]*url\(['"]?([^'")]+)['"]?\)/);

    assert.ok(match, 'The fox icon CSS must declare a background image.');
    assert.equal(path.isAbsolute(match[1]), false, 'The icon URL must work without a preview server.');
    assert.equal(
      fs.existsSync(path.resolve(path.dirname(file), match[1])),
      true,
      `The fox icon asset must exist for ${file}.`
    );
  });
}

test('production shell exposes a hide-to-background button beside settings', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');
  const toolbarStart = html.indexOf('<div class="toolbar">');
  const filtersStart = html.indexOf('<div class="filters"', toolbarStart);
  const toolbar = html.slice(toolbarStart, filtersStart);
  const minimizeIndex = toolbar.indexOf('id="minimizeButton"');
  const settingsIndex = toolbar.indexOf('id="settingsButton"');

  assert.notEqual(toolbarStart, -1);
  assert.notEqual(filtersStart, -1);
  assert.notEqual(minimizeIndex, -1);
  assert.ok(minimizeIndex < settingsIndex);
  assert.match(toolbar, /id="minimizeButton"[^>]*title="隐藏到后台"/);
  assert.match(html, /hhQ\('#minimizeButton'\)\.onclick=hidePanel/);
  assert.match(html, /\.toolbar\{[^}]*grid-template-columns:minmax\(0,1fr\) 42px 42px/);
});
