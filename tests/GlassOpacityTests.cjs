const test = require('node:test');
const assert = require('node:assert/strict');

const glassOpacity = require('../src/HuahaiClipboard.App/Assets/Web/glass-opacity.js');

test('background transparency changes the glass material without fading content', () => {
  const properties = new Map();
  const style = {
    setProperty(name, value) {
      properties.set(name, value);
    }
  };

  const normalized = glassOpacity.apply(style, 65);

  assert.equal(normalized, 0.65);
  assert.equal(properties.get('--glass-material-opacity'), '0.65');
  assert.equal(properties.get('--glass-material-opacity-percent'), '65%');
  assert.equal(properties.has('opacity'), false);
});
