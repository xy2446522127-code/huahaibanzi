(function (global) {
  'use strict';

  function formatKey(event) {
    const parts = [];
    if (event.ctrlKey) parts.push('Ctrl');
    if (event.altKey) parts.push('Alt');
    if (event.shiftKey) parts.push('Shift');
    if (event.metaKey) parts.push('Win');

    if (['Control', 'Alt', 'Shift', 'Meta'].includes(event.key)) return '';
    const key = event.code && event.code.startsWith('Numpad')
      ? event.code
      : event.key === ' '
      ? 'Space'
      : event.key.length === 1
        ? event.key.toUpperCase()
        : event.key;
    parts.push(key);
    return parts.join(' + ');
  }

  function modifierParts(event = {}) {
    const parts = [];
    if (event.ctrlKey) parts.push('Ctrl');
    if (event.altKey) parts.push('Alt');
    if (event.shiftKey) parts.push('Shift');
    if (event.metaKey) parts.push('Win');
    return parts;
  }

  function mouseGestureForButton(button, event = {}) {
    const mouse = {
      0: '鼠标左键',
      1: '鼠标中键',
      2: '鼠标右键',
      3: '鼠标侧键 1',
      4: '鼠标侧键 2'
    }[button];
    if (!mouse) return null;
    const parts = modifierParts(event);
    if ((button === 0 || button === 2) && parts.length === 0) return null;
    parts.push(mouse);
    return parts.join(' + ');
  }

  function wheelGesture(deltaY, event = {}) {
    if (!Number.isFinite(deltaY) || deltaY === 0) return null;
    const parts = modifierParts(event);
    parts.push(deltaY < 0 ? '鼠标滚轮上' : '鼠标滚轮下');
    return parts.join(' + ');
  }

  const api = Object.freeze({ formatKey, mouseGestureForButton, wheelGesture });
  global.HuahaiShortcutInput = api;
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
