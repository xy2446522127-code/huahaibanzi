const test = require('node:test');
const assert = require('node:assert/strict');

const capsule = require('../src/HuahaiClipboard.App/Assets/Web/capsule-geometry.js');

const viewport = { width: 1180, height: 760 };
const size = { width: 180, height: 44 };

test('capsule snaps to the closest allowed left, right, or top edge', () => {
  assert.deepEqual(capsule.snapCapsuleGeometry({ left: 20, top: 340 }, size, viewport), { side: 'left', offset: 340 });
  assert.deepEqual(capsule.snapCapsuleGeometry({ left: 970, top: 340 }, size, viewport), { side: 'right', offset: 340 });
  assert.deepEqual(capsule.snapCapsuleGeometry({ left: 470, top: 12 }, size, viewport), { side: 'top', offset: 470 });
});

test('capsule edge positions clamp the stored offset inside the visible desktop', () => {
  assert.deepEqual(capsule.positionForCapsuleSide({ side: 'left', offset: -20 }, size, viewport), { left: 0, top: 0 });
  assert.deepEqual(capsule.positionForCapsuleSide({ side: 'right', offset: 900 }, size, viewport), { left: 1000, top: 716 });
  assert.deepEqual(capsule.positionForCapsuleSide({ side: 'top', offset: 1400 }, size, viewport), { left: 1000, top: 0 });
});

test('the product shell persists each paper capsule and suppresses restore after a drag', () => {
  const fs = require('node:fs');
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');

  assert.match(html, /const capsulePositionKey = 'huahai\.todo\.capsule-positions-v1';/);
  assert.match(html, /function applyCapsuleGeometry\(entry, geometry\)/);
  assert.match(html, /function installCapsuleDrag\(entry\)/);
  assert.match(html, /HuahaiCapsuleGeometry\.snapCapsuleGeometry/);
  assert.match(html, /cap\.onclick\s*=\s*event\s*=>\s*\{[\s\S]*drag\?\.moved[\s\S]*restorePaper\(entry\);/);
  assert.match(html, /\w+\.target\.closest\('\.floating-paper,\.paper-capsule'\)/);
  assert.match(html, /\.paper-capsule\{[^}]*grid-template-columns:minmax\(0,1fr\)[^}]*padding:0 14px/);
  assert.match(html, /\.paper-capsule strong\{[^}]*font-size:14px/);
  assert.match(html, /cap\.innerHTML=`<strong>\$\{safe\(entry\.window\.querySelector\('\.floating-title'\)\.value\|\|'无标题纸片'\)\}<\/strong>`/);
  assert.match(html, /entry\.capsuleMode\s*=\s*true/);
  assert.match(html, /function updateCapsuleButton\(entry\)/);
  assert.match(html, /if\(entry\.capsuleMode\)\{closePaper\(entry\);return;\}/);
  assert.match(html, /restorePaper\(entry\);[\s\S]*updateCapsuleButton\(entry\)/);
});
