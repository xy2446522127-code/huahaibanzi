(function (global) {
  'use strict';

  function formatKey(event) {
    const parts = [];
    if (event.ctrlKey) parts.push('Ctrl');
    if (event.altKey) parts.push('Alt');
    if (event.shiftKey) parts.push('Shift');
    if (event.metaKey) parts.push('Win');

    if (['Control', 'Alt', 'Shift', 'Meta'].includes(event.key)) return '';
    let key = event.code && event.code.startsWith('Numpad')
      ? event.code
      : event.key === ' '
      ? 'Space'
      : event.key.length === 1
        ? event.key.toUpperCase()
        : event.key;
    key = ({ ArrowLeft: 'Left', ArrowUp: 'Up', ArrowRight: 'Right', ArrowDown: 'Down' })[key] || key;
    const functionKey = /^F(?:[1-9]|1\d|2[0-4])$/i.test(key);
    const characterKey = /^[A-Z0-9]$/.test(key);
    const numpadDigit = /^Numpad[0-9]$/.test(key);
    const namedKeys = new Set([
      'Space', 'Tab', 'Enter', 'Return', 'Esc', 'Escape', 'Left', 'Up', 'Right', 'Down',
      'Home', 'End', 'PageUp', 'PageDown', 'Insert', 'Delete', 'Backspace', 'CapsLock',
      'PrintScreen', 'Pause', 'NumpadMultiply', 'NumpadAdd', 'NumpadSubtract',
      'NumpadDecimal', 'NumpadDivide', 'VolumeMute', 'VolumeDown', 'VolumeUp',
      'MediaNextTrack', 'MediaPreviousTrack', 'MediaStop', 'MediaPlayPause'
    ]);
    if (!functionKey && !characterKey && !numpadDigit && !namedKeys.has(key)) return '';
    if (parts.length === 0 && !functionKey) return `双击 ${key}`;
    if (parts.length === 0) return key;
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
