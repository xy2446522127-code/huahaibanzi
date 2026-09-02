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
    const end = html.indexOf('</script>', start);

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

test('production shell exposes the approved tabs and toolbar controls', () => {
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
  assert.match(html, /product-tabs/);
  assert.match(html, /id=\"clipboardTab\"/);
  assert.match(html, /id=\"todoTab\"/);
  assert.match(html, /\.toolbar\{[^}]*grid-template-columns:minmax\(0,1fr\) 42px 42px/);
});

test('optional update banner cannot reflow the list or bottom copy controls', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');
  assert.match(
    html,
    /\.panel-main\{[^}]*grid-template-areas:"header"\s+"toolbar"\s+"update"\s+"filters"\s+"list"\s+"footer"/,
  );
  for (const [selector, area] of [
    ['.panel-header', 'header'],
    ['.toolbar', 'toolbar'],
    ['.update-banner', 'update'],
    ['.filters', 'filters'],
    ['.record-list', 'list'],
    ['.panel-footer', 'footer'],
  ]) {
    assert.match(html, new RegExp(`${selector.replace('.', '\\.') }\\{[^}]*grid-area:${area}`), `${selector} grid area`);
  }
  assert.match(html, /\.record-list\{[^}]*min-height:0[^}]*overflow-y:auto/);
  assert.match(html, /\.panel-footer\{[^}]*align-self:end/);
});

test('toolbar hide button renders the approved accessible rounded svg line', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /id="minimizeButton"[^>]*aria-label="隐藏到后台"/);
  assert.match(html, /<svg class="minimize-glyph"[^>]*viewBox="0 0 24 24"[^>]*aria-hidden="true"><path d="M4 12h16"\/><\/svg>/);
  assert.match(html, /\.minimize-glyph path\{[^}]*stroke-linecap:round/);
});

test('production shell renders lazy square image thumbnails and the approved original-shape ruby pin', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');
  const bridge = fs.readFileSync(
    'src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs',
    'utf8'
  );

  assert.match(html, /id="pinOutlinePath"/);
  assert.match(html, /id="pinSolidPath"/);
  assert.match(html, /id="rubyPinFill"/);
  assert.match(html, /href="\$\{item\.pin\?'#pinSolidPath':'#pinOutlinePath'\}"/);
  assert.match(html, /\.row-action\.pin\.on\{[^}]*background:transparent/);
  assert.match(html, /\.row-action\.fav\.on\{[^}]*background:transparent/);
  assert.match(html, /\.record-thumbnail\{[^}]*width:33px[^}]*height:33px[^}]*aspect-ratio:1[^}]*object-fit:cover/);
  assert.match(html, /postNative\('requestThumbnail',\{id\}\)/);
  assert.match(html, /data\.type==='thumbnail'/);
  assert.match(html, /thumbnail-fallback/);
  assert.match(bridge, /ClipboardRecordDisplay\.From\(record\)/);
  assert.match(bridge, /thumbnailAvailable = display\.HasThumbnail/);
  assert.match(bridge, /type = "thumbnail"/);
  assert.doesNotMatch(bridge, /thumbnailAvailable\s*=\s*record\.PreviewAssetPath/);
});

test('production pin keeps the approved silhouette while mirroring and rotating clockwise 130 degrees', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /\.pin-glyph\{[^}]*transform:rotate\(130deg\) scaleX\(-1\)/);
  assert.doesNotMatch(html, /\.pin-glyph\{[^}]*rotate\(-12deg\)/);
  assert.match(html, /id="pinSolidPath"/);
  assert.match(html, /id="pinOutlinePath"/);
});

test('preview paper reuses todo surface tokens and exposes paper gestures', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');
  assert.match(html, /\.preview-window\.glass\{[^}]*border-radius:23px/);
  assert.match(html, /id=\"previewResize\"/);
  assert.match(html, /function beginPreviewResize/);
  assert.match(html, /function beginPreviewDrag/);
  assert.match(html, /class=\"preview-board preview-window glass\"/);
  assert.match(html, /class=\"preview-header preview-titlebar\"/);
  assert.match(html, /class=\"resize-handle preview-resizer\"/);
  assert.match(html, /preview-topmost-pin/);
  assert.doesNotMatch(html, /<button class="preview-icon[^>]*>⌖<\/button>/);
});

test('web prototype opens one shared preview window from the record context menu', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /<script src="preview-prototype\.js"><\/script>/);
  assert.match(html, /<section class="preview-board preview-window glass"[^>]*id="previewWindow"/);
  assert.match(html, /recordList\.addEventListener\('contextmenu'[\s\S]*openPreviewRecord\(item,'右键'\)/);
  assert.doesNotMatch(html, /toast\('原型中右键会打开完整内容预览'\)/);
});

test('shared preview renders final paper icons without a mutation observer patch', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /id="previewTopmost"/);
  assert.match(html, /previewTopmost:'<svg class="preview-topmost-pin" viewBox="0 -52 64 36"/);
  assert.match(html, /id="previewHide"[\s\S]*M5 12h14/);
  assert.match(html, /id="previewClose"[\s\S]*m18 6-12 12M6 6l12 12/);
});

test('preview topmost reuses the todo paper pin source and styling', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /previewTopmost:'<svg class="preview-topmost-pin" viewBox="0 -52 64 36"/);
  assert.match(html, /getElementById\('previewTopmost'\)\?\.classList\.contains\('active'\)\?'#pinSolidPath':'#pinOutlinePath'/);
  assert.match(html, /\.preview-topmost-pin\{[^}]*transform:rotate\(130deg\) scaleX\(-1\)/);
  assert.match(html, /\.preview-icon\.active\.pin[^}]*color:#ff4968/);
});

test('shared preview exposes note save close restore drag and resize controls', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  for (const id of [
    'previewNoteToggle',
    'previewNoteEditor',
    'previewNoteSave',
    'previewSave',
    'previewDiscard',
    'previewCopy',
    'previewReopen',
    'previewDrag',
    'previewResize',
  ]) {
    assert.match(html, new RegExp('id="' + id + '"'), id);
  }
  assert.match(html, /savePreviewRecord/);
  assert.match(html, /beginPreviewDrag/);
  assert.match(html, /beginPreviewResize/);
});

test('native preview host keeps transparent corners', () => {
  const cs = fs.readFileSync('src/HuahaiClipboard.App/Presentation/Windows/ContentPreviewWindow.xaml.cs', 'utf8');
  assert.match(cs, /previewWebView\.DefaultBackgroundColor\s*=\s*Colors\.Transparent/);
  assert.match(cs, /DwmSetWindowAttribute\(handle, 33/);
  assert.match(cs, /DwmSetWindowAttribute\(handle, 34/);
});
