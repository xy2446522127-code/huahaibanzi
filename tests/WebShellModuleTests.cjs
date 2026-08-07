const test = require('node:test');
const assert = require('node:assert/strict');

const interaction = require('../src/HuahaiClipboard.App/Assets/Web/interaction-contract.js');
const router = require('../src/HuahaiClipboard.App/Assets/Web/shell-router.js');

test('interaction adapter exposes every WebView-visible control exactly once', () => {
  const staticIds = Object.keys(interaction.staticControls);
  assert.equal(staticIds.length, 41);
  assert.deepEqual(Object.values(interaction.filterControls).sort(), [
    'filter.all', 'filter.favorites', 'filter.file', 'filter.image', 'filter.link', 'filter.text',
  ].sort());
  assert.deepEqual(interaction.recordControls, {
    row: 'record.copy',
    pin: 'record.pin',
    favorite: 'record.favorite',
    delete: 'record.delete',
  });
  assert.equal(staticIds.length + Object.keys(interaction.filterControls).length + Object.keys(interaction.recordControls).length, 51);
});

test('shell router accepts only approved settings pages and returns stable hashes', () => {
  assert.deepEqual(router.parseHash('#panel'), { surface: 'panel', page: null });
  assert.deepEqual(router.parseHash('#settings/storage'), { surface: 'settings', page: 'storage' });
  assert.deepEqual(router.parseHash('#settings/not-real'), { surface: 'panel', page: null });
  assert.equal(router.settingsHash('about'), '#settings/about');
  assert.equal(router.settingsHash('not-real'), '#settings/appearance');
  assert.equal(router.panelHash(), '#panel');
});
