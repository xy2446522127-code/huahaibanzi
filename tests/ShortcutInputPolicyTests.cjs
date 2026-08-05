const test = require('node:test');
const assert = require('node:assert/strict');

const shortcutInput = require('../src/HuahaiClipboard.App/Assets/Web/shortcut-input.js');

test('mouse shortcut is captured after listening starts even when pointer stays on capture control', () => {
  assert.equal(shortcutInput.mouseGestureForButton(1), '鼠标中键');
  assert.equal(shortcutInput.mouseGestureForButton(3), '鼠标侧键 1');
  assert.equal(shortcutInput.mouseGestureForButton(4), '鼠标侧键 2');
  assert.equal(shortcutInput.mouseGestureForButton(0), null);
});

test('keyboard shortcut formatting keeps supported modifiers and the final key', () => {
  assert.equal(shortcutInput.formatKey({
    ctrlKey: true,
    altKey: true,
    shiftKey: false,
    metaKey: false,
    key: 'h'
  }), 'Ctrl + Alt + H');
  assert.equal(shortcutInput.formatKey({
    ctrlKey: false,
    altKey: false,
    shiftKey: false,
    metaKey: false,
    key: 'F8'
  }), 'F8');
});
