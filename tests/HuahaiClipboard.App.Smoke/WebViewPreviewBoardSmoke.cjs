const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');

const shell = readFileSync(resolve(__dirname, '../../src/HuahaiClipboard.App/Assets/Web/product-shell.html'), 'utf8');

assert.match(shell, /get\('surface'\)===['"]preview['"]/, 'The product shell must expose the preview surface route.');
assert.match(shell, /previewMetrics/, 'The preview surface must calculate live long-content metrics.');
assert.match(shell, /item\.text\.length\s*>\s*120/, 'Long text cards must expose their length in metadata.');
assert.match(shell, /previewConfirm/, 'Dirty preview close and record replacement must present a confirmation state.');
assert.match(shell, /mode:reason/, 'The confirmation save action must preserve the requested close or switch operation.');

console.log('WebView preview board shell contract passed.');
