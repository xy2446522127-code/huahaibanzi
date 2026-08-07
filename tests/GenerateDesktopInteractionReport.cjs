const fs = require('node:fs');
const path = require('node:path');

const [contractPath, webReportPath, nativeEvidencePath, outputPath] = process.argv.slice(2);
if (!contractPath || !webReportPath || !nativeEvidencePath || !outputPath) {
  throw new Error('Usage: node GenerateDesktopInteractionReport.cjs <contract> <web-report> <native-evidence> <output>');
}

const contract = JSON.parse(fs.readFileSync(contractPath, 'utf8'));
const web = JSON.parse(fs.readFileSync(webReportPath, 'utf8'));
const native = JSON.parse(fs.readFileSync(nativeEvidencePath, 'utf8'));
if (web.contract_revision !== contract.contract_revision || web.console_errors?.length || web.unexplained_controls?.length) {
  throw new Error('The Web interaction report is stale or not clean.');
}
if (native.contract_revision !== contract.contract_revision) {
  throw new Error('The native interaction evidence is stale.');
}
if (!native.commit || !Array.isArray(native.results)) {
  throw new Error('The native interaction evidence is missing its commit or results.');
}

const webResults = new Map(web.results.map(result => [result.control_id, result]));
const nativeResults = new Map(native.results.map(result => [result.control_id, result]));
const results = contract.controls.map(control => {
  const platformOnly = control.fixture.route.startsWith('huahai://');
  const webResult = webResults.get(control.control_id);
  if (!platformOnly && (!webResult || webResult.status !== 'passed' || !webResult.reached || !webResult.triggered || !webResult.matched)) {
    throw new Error(`Missing clean Web evidence for ${control.control_id}`);
  }

  const result = nativeResults.get(control.control_id);
  if (!result) throw new Error(`Missing native evidence for ${control.control_id}`);
  if (result.status !== 'passed' || !result.reached || !result.triggered || !result.matched || result.behavior !== 'native') {
    throw new Error(`Native evidence did not pass for ${control.control_id}`);
  }
  if (!Array.isArray(result.evidence) || result.evidence.length === 0) {
    throw new Error(`Native evidence has no observed adapter for ${control.control_id}`);
  }
  return result;
});

const unexpected = native.results
  .map(result => result.control_id)
  .filter(controlId => !contract.controls.some(control => control.control_id === controlId));
const report = {
  version: 2,
  target: 'desktop',
  contract_revision: contract.contract_revision,
  commit: native.commit,
  results,
  unexplained_controls: [...new Set([...(native.unexplained_controls || []), ...unexpected])],
  console_errors: native.console_errors || [],
  evidence_summary: native.evidence_summary || {},
  runner: 'tests/GenerateDesktopInteractionReport.cjs',
};

if (report.unexplained_controls.length || report.console_errors.length) {
  throw new Error('Native evidence contains unexplained controls or console errors.');
}

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
process.stdout.write(JSON.stringify({ controls: results.length, passed: results.length, revision: report.contract_revision, commit: report.commit }));
