const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const runner = path.resolve('tests/GenerateDesktopInteractionReport.cjs');

function fixture() {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'HuahaiClipboard.DesktopReport.'));
  const paths = {
    root,
    contract: path.join(root, 'contract.json'),
    web: path.join(root, 'web.json'),
    native: path.join(root, 'native.json'),
    output: path.join(root, 'desktop.json'),
  };
  const revision = 'test-revision';
  fs.writeFileSync(paths.contract, JSON.stringify({ contract_revision: revision, controls: [
    { control_id: 'web.control', fixture: { route: 'https://app.huahai.local/Web/product-shell.html#panel' } },
    { control_id: 'global.control', fixture: { route: 'huahai://background' } },
  ] }));
  fs.writeFileSync(paths.web, JSON.stringify({ contract_revision: revision, results: [
    { control_id: 'web.control', status: 'passed', reached: true, triggered: true, matched: true },
  ], unexplained_controls: [], console_errors: [] }));
  return paths;
}

function run(paths) {
  return spawnSync(process.execPath, [runner, paths.contract, paths.web, paths.native, paths.output], {
    encoding: 'utf8',
  });
}

test('desktop report preserves real machine results and derives evidence counts', () => {
  const paths = fixture();
  try {
    fs.writeFileSync(paths.native, JSON.stringify({
      contract_revision: 'test-revision',
      commit: 'abc1234',
      results: [
        { control_id: 'web.control', status: 'passed', reached: true, triggered: true, matched: true, behavior: 'native', evidence: ['real-webview-smoke'] },
        { control_id: 'global.control', status: 'passed', reached: true, triggered: true, matched: true, behavior: 'native', evidence: ['real-global-input-smoke'] },
      ],
      evidence_summary: { core_tests: { passed: 95, failed: 0 }, tray_shell: { passed: 3, failed: 0 } },
    }));
    const result = run(paths);
    assert.equal(result.status, 0, result.stderr || result.stdout);
    const report = JSON.parse(fs.readFileSync(paths.output, 'utf8'));
    assert.deepEqual(report.results.map(item => item.evidence[0]), ['real-webview-smoke', 'real-global-input-smoke']);
    assert.equal(report.evidence_summary.core_tests.passed, 95);
    assert.equal(report.commit, 'abc1234');
  } finally {
    fs.rmSync(paths.root, { recursive: true, force: true });
  }
});

test('desktop report rejects a native result that was not observed', () => {
  const paths = fixture();
  try {
    fs.writeFileSync(paths.native, JSON.stringify({
      contract_revision: 'test-revision',
      commit: 'abc1234',
      results: [
        { control_id: 'web.control', status: 'passed', reached: true, triggered: true, matched: true, behavior: 'native', evidence: ['real-webview-smoke'] },
      ],
      evidence_summary: {},
    }));
    const result = run(paths);
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /Missing native evidence for global\.control/);
  } finally {
    fs.rmSync(paths.root, { recursive: true, force: true });
  }
});
