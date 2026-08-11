const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const source = fs.readFileSync(
  'src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs',
  'utf8'
);

test('global summon input is registered before history maintenance and projection loading', () => {
  const registration = source.indexOf('globalInputService = new GlobalInputService(');
  const prune = source.indexOf('compositionRoot.RetentionService.ApplyAsync(');
  const load = source.indexOf('await panelViewModel.LoadAsync();', prune);
  const collectOrphans = source.indexOf('compositionRoot.ImageStore.DeleteUnreferencedAsync(', load);
  const protectImages = source.indexOf('compositionRoot.ImageStore.ProtectLegacyFilesAsync(');

  assert.ok(registration >= 0, 'global input registration is missing');
  assert.ok(prune >= 0 && load >= 0 && collectOrphans >= 0 && protectImages >= 0, 'runtime maintenance sequence is missing');
  assert.ok(registration < prune, 'history pruning delays global input registration');
  assert.ok(registration < load, 'history projection loading delays global input registration');
  assert.ok(registration < protectImages, 'legacy image protection delays global input registration');
  assert.ok(load < collectOrphans, 'orphan collection must use the loaded authoritative history');
});
