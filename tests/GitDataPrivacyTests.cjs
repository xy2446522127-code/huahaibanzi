const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const root = path.resolve(__dirname, '..');
const gitignore = fs.readFileSync(path.join(root, '.gitignore'), 'utf8');

function checkIgnored(sample) {
  const result = spawnSync('git', ['check-ignore', '--no-index', '-q', '--', sample], {
    cwd: root,
    encoding: 'utf8'
  });
  assert.equal(result.status, 0, `${sample} must be ignored by .gitignore`);
}

test('runtime user data and installer swap directories are explicitly ignored', () => {
  assert.match(gitignore, /^\*\*\/Data\/$/m);
  assert.match(gitignore, /^\*\*\/\.HuahaiClipboard-install-\*\/$/m);
  assert.match(gitignore, /^\*\*\/\.HuahaiClipboard-backup-\*\/$/m);

  checkIgnored('Data/S-1-5-21-1000/history.dat');
  checkIgnored('Data/S-1-5-21-1000/settings.json');
  checkIgnored('Data/S-1-5-21-1000/images/clipboard.png');
  checkIgnored('.HuahaiClipboard-install-1234567890abcdef/Data/history.dat');
  checkIgnored('.HuahaiClipboard-backup-1234567890abcdef/Data/history.dat');
});

test('tracked repository paths contain no runtime Data directory or user history payload', () => {
  const result = spawnSync('git', ['ls-files', '-z'], { cwd: root });
  assert.equal(result.status, 0);
  const tracked = result.stdout.toString('utf8').split('\0').filter(Boolean);

  for (const file of tracked) {
    assert.doesNotMatch(file, /(^|[\\/])Data([\\/]|$)/i, `runtime Data path is tracked: ${file}`);
    assert.doesNotMatch(file, /(^|[\\/])(history|settings|window-positions)\.dat?$/i, `user payload is tracked: ${file}`);
  }
});
