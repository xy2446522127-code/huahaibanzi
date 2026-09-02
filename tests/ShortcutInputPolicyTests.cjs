const test = require('node:test');
const assert = require('node:assert/strict');

const shortcutInput = require('../src/HuahaiClipboard.App/Assets/Web/shortcut-input.js');

test('mouse shortcut is captured after listening starts even when pointer stays on capture control', () => {
  assert.equal(shortcutInput.mouseGestureForButton(1), '鼠标中键');
  assert.equal(shortcutInput.mouseGestureForButton(3), '鼠标侧键 1');
  assert.equal(shortcutInput.mouseGestureForButton(4), '鼠标侧键 2');
  assert.equal(shortcutInput.mouseGestureForButton(0), null);
  assert.equal(shortcutInput.mouseGestureForButton(0, { ctrlKey: true }), 'Ctrl + 鼠标左键');
  assert.equal(shortcutInput.mouseGestureForButton(2, { altKey: true }), 'Alt + 鼠标右键');
  assert.equal(shortcutInput.wheelGesture(-120), '鼠标滚轮上');
  assert.equal(shortcutInput.wheelGesture(120), '鼠标滚轮下');
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
  assert.equal(shortcutInput.formatKey({
    ctrlKey: true,
    altKey: false,
    shiftKey: false,
    metaKey: false,
    key: '1',
    code: 'Numpad1'
  }), 'Ctrl + Numpad1');
});

test('keyboard shortcut formatting maps plain named keys to double-tap gestures', () => {
  const event = key => ({ ctrlKey: false, altKey: false, shiftKey: false, metaKey: false, key });
  assert.equal(shortcutInput.formatKey(event('Enter')), '双击 Enter');
  assert.equal(shortcutInput.formatKey(event('Tab')), '双击 Tab');
  assert.equal(shortcutInput.formatKey(event('ArrowLeft')), '双击 Left');
  assert.equal(shortcutInput.formatKey({ ...event('Enter'), ctrlKey: true, code: 'NumpadEnter' }), '');
  assert.equal(shortcutInput.formatKey({ ...event('Enter'), ctrlKey: true }), 'Ctrl + Enter');
});

test('keyboard shortcut formatting turns plain keys into double-tap gestures', () => {
  assert.equal(shortcutInput.formatKey({ ctrlKey: false, altKey: false, shiftKey: false, metaKey: false, key: 'a' }), '双击 A');
  assert.equal(shortcutInput.formatKey({ ctrlKey: false, altKey: false, shiftKey: false, metaKey: false, key: ' ' }), '双击 Space');
});
