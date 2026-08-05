(function (global) {
  'use strict';

  function formatKey(event) {
    const parts = [];
    if (event.ctrlKey) parts.push('Ctrl');
    if (event.altKey) parts.push('Alt');
    if (event.shiftKey) parts.push('Shift');
    if (event.metaKey) parts.push('Win');

    if (['Control', 'Alt', 'Shift', 'Meta'].includes(event.key)) return '';
    parts.push(event.key === ' '
      ? 'Space'
      : event.key.length === 1
        ? event.key.toUpperCase()
        : event.key);
    return parts.join(' + ');
  }

  function mouseGestureForButton(button) {
    return {
      1: '鼠标中键',
      3: '鼠标侧键 1',
      4: '鼠标侧键 2'
    }[button] || null;
  }

  const api = Object.freeze({ formatKey, mouseGestureForButton });
  global.HuahaiShortcutInput = api;
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
