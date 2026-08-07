const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const shellPath = 'src/HuahaiClipboard.App/Assets/Web/product-shell.html';
const html = fs.readFileSync(shellPath, 'utf8');
const interactionModule = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/interaction-contract.js', 'utf8');
const count = (part) => html.split(part).length - 1;
const interactionContract = JSON.parse(fs.readFileSync('.codex/app-product-delivery-interaction-contract.json', 'utf8'));

test('formal product shell uses the approved A petal-flow wordmark in both brand locations', () => {
  assert.equal(count('class="petal-text"'), 2);
  assert.equal(count('<span class="accent">花海</span>剪贴板'), 2);
  assert.match(html, /\.petal-text\{[^}]*font-family:"STKaiti","KaiTi","Microsoft YaHei UI",sans-serif/);
  assert.match(html, /\.petal-text\{[^}]*font-weight:700/);
});

test('settings gear has one live entry and every settings page shares a return path', () => {
  assert.equal(count('id="settingsButton"'), 1);
  assert.match(html, /hhQ\('#settingsButton'\)\.onclick=\(\)=>openSettings\('appearance'\)/);
  assert.equal(count('id="backButton"'), 1);
  assert.match(html, /hhQ\('#backButton'\)\.onclick=\(\)=>closeSettings\(\)/);

  for (const page of ['appearance', 'motion', 'input', 'storage', 'system', 'about']) {
    assert.match(html, new RegExp(`class="nav-button(?: active)?" data-page="${page}"`), `${page} nav`);
    assert.match(html, new RegExp(`class="settings-page(?: active)?" data-page="${page}"`), `${page} page`);
  }
});

test('appearance page exposes proportional panel scaling and a one-click reset', () => {
  assert.equal(count('id="scaleRange"'), 1);
  assert.match(html, /id="scaleRange"[^>]*min="80"[^>]*max="160"[^>]*step="5"[^>]*value="100"/);
  assert.equal(count('id="scaleValue"'), 1);
  assert.equal(count('id="resetScale"'), 1);
  assert.equal(count('id="resizeHandle"'), 1);
  assert.match(html, /hhQ\('#scaleRange'\)\.oninput=/);
  assert.match(html, /hhQ\('#resetScale'\)\.onclick=/);
  assert.match(html, /window\.HuahaiHostScale\.clampPanelScale/);
  assert.match(html, /postNative\('setPanelScale'/);
});

test('about page uses the production update bridge while keeping preview simulation offline', () => {
  assert.equal(count('id="updateAutoToggle"'), 1);
  assert.equal(count('id="checkUpdateButton"'), 1);
  assert.equal(count('id="updateStatus"'), 1);
  assert.equal(count('id="releaseButton"'), 1);
  assert.equal(count('id="installUpdateButton"'), 1);
  assert.match(html, /版本 1\.1\.2/);
  assert.match(html, /postNative\('setCheckUpdatesOnStartup'/);
  assert.match(html, /postNative\('checkUpdate'/);
  assert.match(html, /postNative\('installUpdate'/);
  assert.match(html, /postNative\('openRelease'/);
  assert.match(html, /data\.type==='updateStatus'/);
  assert.match(html, /GitHub Release/);
  assert.match(html, /检查中/);
  assert.match(html, /发现新版本/);
  assert.match(html, /当前已是最新版本/);
  assert.doesNotMatch(html, /\bfetch\s*\(/);
  assert.doesNotMatch(html, /XMLHttpRequest/);
});

test('every visible prototype control has an explicit interaction binding', () => {
  for (const id of [
    'updateAutoToggle',
    'checkUpdateButton',
    'releaseButton',
    'scaleRange',
    'resetScale'
  ]) {
    assert.match(html, new RegExp(`hhQ\\('#${id}'\\)\\.(?:onclick|oninput)=`), id);
  }
});

test('web preview uses the same panel drag policy for a real movable interaction', () => {
  assert.match(html, /function beginPanelDrag\(event\)/);
  assert.match(html, /window\.HuahaiPanelDrag\.install\(hhQ\('#glassPanel'\),beginPanelDrag\)/);
  assert.match(html, /window\.HuahaiPanelDrag\.previewPosition/);
  assert.match(html, /panel\.style\.left=/);
  assert.match(html, /panel\.style\.top=/);
});

test('localhost preview cannot be mistaken for the native WebView host', () => {
  assert.match(html, /window\.HuahaiHostScale\.isNativeShellHost\(window\.location,window\.chrome\)/);
  assert.doesNotMatch(html, /window\.chrome && window\.chrome\.webview \? window\.chrome\.webview : null/);
});

test('every WebView-visible contract control is tagged on the real product shell', () => {
  const visibleControls = interactionContract.controls.filter(control =>
    control.fixture.route.startsWith('https://app.huahai.local/Web/product-shell.html'),
  );
  assert.equal(visibleControls.length, 54);
  assert.match(interactionModule, /data-apd-control-id/);
  for (const control of visibleControls) {
    assert.ok((html + interactionModule).includes(`'${control.control_id}'`), control.control_id);
  }
});

test('panel and settings hashes are real deep links with browser history support', () => {
  assert.match(html, /function applyRouteFromHash\(\)/);
  assert.match(html, /window\.addEventListener\('hashchange',applyRouteFromHash\)/);
  assert.match(html, /window\.HuahaiShellRouter\.settingsHash\(page\)/);
  assert.match(html, /window\.HuahaiShellRouter\.panelHash\(\)/);
});
