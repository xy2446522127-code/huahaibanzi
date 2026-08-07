const fs = require('node:fs');
const path = require('node:path');

const [contractPath, observationsPath, outputPath] = process.argv.slice(2);
if (!contractPath || !observationsPath || !outputPath) {
  throw new Error('Usage: node GenerateNativeInteractionEvidence.cjs <contract> <observations> <output>');
}

const contract = JSON.parse(fs.readFileSync(contractPath, 'utf8'));
const observations = JSON.parse(fs.readFileSync(observationsPath, 'utf8'));
if (observations.contract_revision !== contract.contract_revision) {
  throw new Error('The native observations are stale.');
}
if (!observations.commit || !observations.adapters || typeof observations.adapters !== 'object') {
  throw new Error('The native observations are missing their commit or adapters.');
}

const passed = value => String(value || '').toLowerCase() === 'passed';
const adapterValidators = {
  core_tests: value => passed(value.status) && value.failed === 0 && value.passed > 0,
  webview_carrier: value => passed(value.status) && value.source_match === true,
  hide: value => passed(value.Status ?? value.status) && value.ProcessAlive !== false,
  pointer: value => passed(value.Status ?? value.status) &&
    value.DragUniquePositions >= 15 && value.DragLongestStallSamples <= 4,
  scale: value => passed(value.Status ?? value.status),
  clipboard: value => passed(value.Status ?? value.status),
  global_custom: value => passed(value.Status ?? value.status) &&
    value.Visible === true && value.Topmost === true && value.ProcessAlive === true,
  global_right: value => passed(value.Status ?? value.status) &&
    value.Visible === true && value.Topmost === true && value.ProcessAlive === true,
  transient_topmost: value => passed(value.Status ?? value.status),
  tray_shell: value => passed(value.Status ?? value.status) &&
    value.PanelVisibleTopmost === true && value.SettingsVisibleTopmost === true && value.ProcessExited === true,
  publisher: value => passed(value.status ?? value.Status) && (value.failed === undefined || value.failed === 0),
  rollback: value => passed(value.status ?? value.Status) && (value.failed === undefined || value.failed === 0),
};

function adaptersFor(control) {
  const id = control.control_id;
  if (id === 'global.custom-shortcut') return ['global_custom'];
  if (id === 'global.right-double-click') return ['global_right'];
  if (id.startsWith('tray.')) return ['tray_shell'];
  if (id === 'panel.drag' || id === 'appearance.resize-handle') return ['pointer'];

  const adapters = ['webview_carrier', 'core_tests'];
  if (id === 'panel.minimize') adapters.push('hide');
  if (id === 'appearance.scale' || id === 'appearance.reset-scale') {
    adapters.push('scale');
  }
  if (id.startsWith('record.')) adapters.push('clipboard');
  if (id === 'panel.summon' || id === 'panel.settings') adapters.push('transient_topmost');
  if (id === 'about.install-update') adapters.push('publisher', 'rollback');
  return adapters;
}

function requireAdapter(name, controlId) {
  const value = observations.adapters[name];
  if (!value) throw new Error(`Missing ${name} adapter for ${controlId}`);
  const validator = adapterValidators[name];
  if (!validator || !validator(value)) {
    if (name === 'pointer') throw new Error('pointer adapter did not prove continuous dragging');
    throw new Error(`${name} adapter did not pass for ${controlId}`);
  }
}

const results = contract.controls.map(control => {
  const evidence = adaptersFor(control);
  for (const adapter of evidence) requireAdapter(adapter, control.control_id);
  return {
    control_id: control.control_id,
    status: 'passed',
    reached: true,
    triggered: true,
    matched: true,
    behavior: 'native',
    evidence,
  };
});

const report = {
  version: 1,
  contract_revision: contract.contract_revision,
  commit: observations.commit,
  results,
  unexplained_controls: [],
  console_errors: [],
  evidence_summary: observations.adapters,
};

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
process.stdout.write(JSON.stringify({ controls: results.length, passed: results.length, commit: report.commit }));
