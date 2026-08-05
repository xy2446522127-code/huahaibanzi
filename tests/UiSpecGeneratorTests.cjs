const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const { pathToFileURL } = require('node:url');

let renderCss;
let renderWpf;
let spec;
let validateSpec;

test.before(async () => {
  ({ validateSpec, renderCss, renderWpf } = await import(
    pathToFileURL(path.resolve('tools/generate-huahai-ui-spec.mjs')).href
  ));
  spec = JSON.parse(fs.readFileSync('ui/huahai-ui-spec.json', 'utf8'));
});

test('approved UI spec validates the locked surfaces and five themes', () => {
  assert.deepEqual(validateSpec(spec), []);
  assert.equal(spec.schemaVersion, 1);
  assert.equal(spec.visualSourceVersion, '1.0.4');
  assert.deepEqual(spec.panel, { width: 430, height: 680, cornerRadius: 29 });
  assert.deepEqual(spec.settings, { width: 820, height: 650 });
  assert.deepEqual(spec.themes.map((theme) => theme.id), [
    'rose-purple',
    'cobalt-blue',
    'emerald-cyan',
    'amber-orange',
    'aurora-cyan-purple',
  ]);
});

test('generator emits stable Web and WPF keys', () => {
  const css = renderCss(spec);
  const xaml = renderWpf(spec);

  assert.match(css, /--huahai-panel-width:430px/);
  assert.match(css, /--huahai-click-duration:620ms/);
  assert.match(xaml, /x:Key="HuahaiPanelWidth">430<\/sys:Double>/);
  assert.match(xaml, /x:Key="HuahaiPanelCornerRadius">29<\/CornerRadius>/);
  assert.match(xaml, /x:Key="HuahaiThemeCount">5<\/sys:Int32>/);
});

test('generator rejects a missing approved theme', () => {
  const invalid = structuredClone(spec);
  invalid.themes.pop();

  assert.deepEqual(validateSpec(invalid), ['themes must contain exactly 5 entries']);
});

test('validator rejects each locked contract mutation', async (context) => {
  const cases = [
    ['schema version', (value) => { value.schemaVersion = 2; }, 'schemaVersion must be 1'],
    ['visual source version', (value) => { value.visualSourceVersion = '1.0.3'; }, 'visualSourceVersion must be 1.0.4'],
    ['panel geometry', (value) => { value.panel.width = 431; }, 'panel geometry does not match the approved contract'],
    ['settings geometry', (value) => { value.settings.height = 651; }, 'settings geometry does not match the approved contract'],
    ['theme ordering', (value) => { [value.themes[0], value.themes[1]] = [value.themes[1], value.themes[0]]; }, 'theme ids or ordering do not match the approved contract'],
    ['ARGB color', (value) => { value.themes[0].accent = '#d786bb'; }, 'themes[0].accent must be an uppercase ARGB color'],
  ];

  for (const [name, mutate, expected] of cases) {
    await context.test(name, () => {
      const invalid = structuredClone(spec);
      mutate(invalid);
      assert.ok(validateSpec(invalid).includes(expected));
    });
  }
});
