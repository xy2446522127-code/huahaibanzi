const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');

const contractPath = '.codex/app-product-delivery-interaction-contract.json';
const contract = JSON.parse(fs.readFileSync(contractPath, 'utf8'));
const controls = contract.controls;

test('interaction contract covers the complete approved control set without placeholders', () => {
  assert.equal(contract.version, 1);
  assert.equal(controls.length, 60);
  assert.ok(controls.some(control => control.control_id === 'settings.home'));
  assert.ok(controls.some(control => control.control_id === 'about.install-update'));
  assert.ok(controls.some(control => control.control_id === 'panel.summon'));
  assert.ok(controls.some(control => control.control_id === 'appearance.resize-handle'));
  assert.ok(controls.some(control => control.control_id === 'input.remove-exclusion'));
  assert.equal(fs.readFileSync(contractPath, 'utf8').includes('?'), false);
  for (const control of controls) {
    assert.ok(control.user_intent.length >= 4, control.control_id);
    assert.ok(control.mock_behavior.observable_result.length >= 4, control.control_id);
    for (const state of ['loading', 'success', 'error', 'disabled']) {
      assert.ok(control.state_contract[state].length >= 4, `${control.control_id}:${state}`);
    }
  }
});

test('panel and settings fixtures use their real desktop dimensions and WebView routes', () => {
  for (const control of controls) {
    const { route, viewport } = control.fixture;
    if (route.includes('#panel')) {
      assert.deepEqual(viewport, { width: 430, height: 680 }, control.control_id);
    }
    if (route.includes('#settings/')) {
      assert.match(route, /^https:\/\/app\.huahai\.local\/Web\/product-shell\.html#settings\//);
      assert.deepEqual(viewport, { width: 820, height: 650 }, control.control_id);
    }
    assert.notEqual(route, 'native://settings');
  }
});

test('every control declares desktop behavior and deterministic fixture semantics', () => {
  for (const control of controls) {
    assert.equal(control.disposition, 'interactive', control.control_id);
    assert.equal(control.targets.desktop.behavior, 'native', control.control_id);
    assert.equal(
      control.targets.web.behavior,
      control.fixture.route.startsWith('huahai://') ? 'simulated-platform-capability' : 'adapted',
      control.control_id
    );
    assert.ok(control.test_id.startsWith('webview.'), control.control_id);
    assert.ok(control.expected.type, control.control_id);
    assert.ok(control.trigger.type, control.control_id);
  }
});
