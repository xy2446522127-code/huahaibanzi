const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const generator = path.resolve('tests/GenerateNativeInteractionEvidence.cjs');

test('native evidence rows are emitted only from passing observed adapters', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'HuahaiClipboard.NativeEvidence.'));
  try {
    const contractPath = path.join(root, 'contract.json');
    const observationsPath = path.join(root, 'observations.json');
    const outputPath = path.join(root, 'native.json');
    fs.writeFileSync(contractPath, JSON.stringify({ contract_revision: 'r1', controls: [
      { control_id: 'theme.rose' },
      { control_id: 'global.right-double-click' },
    ] }));
    fs.writeFileSync(observationsPath, JSON.stringify({
      contract_revision: 'r1',
      commit: 'abc1234',
      adapters: {
        core_tests: { status: 'passed', passed: 95, failed: 0 },
        webview_carrier: { status: 'passed', source_match: true },
        global_right: { Status: 'passed', Visible: true, Topmost: true, ProcessAlive: true },
      },
    }));
    const result = spawnSync(process.execPath, [generator, contractPath, observationsPath, outputPath], { encoding: 'utf8' });
    assert.equal(result.status, 0, result.stderr || result.stdout);
    const evidence = JSON.parse(fs.readFileSync(outputPath, 'utf8'));
    assert.equal(evidence.results.length, 2);
    assert.deepEqual(evidence.results[0].evidence, ['webview_carrier', 'core_tests']);
    assert.deepEqual(evidence.results[1].evidence, ['global_right']);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});

test('native evidence generation fails when an observed adapter failed', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'HuahaiClipboard.NativeEvidence.'));
  try {
    const contractPath = path.join(root, 'contract.json');
    const observationsPath = path.join(root, 'observations.json');
    const outputPath = path.join(root, 'native.json');
    fs.writeFileSync(contractPath, JSON.stringify({ contract_revision: 'r1', controls: [{ control_id: 'panel.drag' }] }));
    fs.writeFileSync(observationsPath, JSON.stringify({
      contract_revision: 'r1', commit: 'abc1234', adapters: {
        pointer: { Status: 'passed', DragUniquePositions: 1, DragLongestStallSamples: 29 },
      },
    }));
    const result = spawnSync(process.execPath, [generator, contractPath, observationsPath, outputPath], { encoding: 'utf8' });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /pointer adapter did not prove continuous dragging/);
  } finally {
    fs.rmSync(root, { recursive: true, force: true });
  }
});
