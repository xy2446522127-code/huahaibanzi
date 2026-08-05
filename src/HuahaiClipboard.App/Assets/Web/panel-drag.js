(function (global, factory) {
  'use strict';
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (global) global.HuahaiPanelDrag = api;
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';
  const holdDurationMs = 0;
  const movementThresholdPx = 5;
  const interactiveSelector = 'button,input,textarea,label,a,.record,.record-list,.filters,.settings-nav,[contenteditable="true"]';

  function isInteractiveTarget(target) {
    return Boolean(target && typeof target.closest === 'function' && target.closest(interactiveSelector));
  }

  function shouldBegin({ button, elapsedMs, distancePx, interactive }) {
    return button === 0 && interactive !== true && elapsedMs >= holdDurationMs && distancePx <= movementThresholdPx;
  }

  function previewPosition({ startLeft, startTop, deltaX, deltaY, panelWidth, panelHeight, surfaceWidth, surfaceHeight }) {
    const maxLeft = Math.max(0, Number(surfaceWidth) - Number(panelWidth));
    const maxTop = Math.max(0, Number(surfaceHeight) - Number(panelHeight));
    return {
      left: Math.max(0, Math.min(maxLeft, Number(startLeft) + Number(deltaX))),
      top: Math.max(0, Math.min(maxTop, Number(startTop) + Number(deltaY)))
    };
  }

  function install(root, beginDrag) {
    if (!root || typeof beginDrag !== 'function') return () => {};
    let pending = null;
    const cancel = () => {
      if (pending?.timer) clearTimeout(pending.timer);
      pending = null;
      root.classList.remove('drag-armed');
    };
    root.addEventListener('pointerdown', event => {
      cancel();
      const interactive = isInteractiveTarget(event.target);
      if (event.button !== 0 || interactive) return;
      pending = { button: event.button, x: event.clientX, y: event.clientY, distancePx: 0, interactive, startedAt: performance.now() };
      root.classList.add('drag-armed');
      if (holdDurationMs === 0) {
        const allowed = shouldBegin({ button: event.button, elapsedMs: 0, distancePx: 0, interactive });
        cancel();
        if (allowed) beginDrag(event);
        return;
      }
      pending.timer = setTimeout(() => {
        if (!pending) return;
        const candidate = pending;
        const allowed = shouldBegin({ button: candidate.button, elapsedMs: holdDurationMs, distancePx: candidate.distancePx, interactive: candidate.interactive });
        cancel();
        if (allowed) beginDrag(event);
      }, holdDurationMs);
    });
    root.addEventListener('pointermove', event => {
      if (!pending) return;
      pending.distancePx = Math.hypot(event.clientX - pending.x, event.clientY - pending.y);
      if (pending.distancePx > movementThresholdPx) cancel();
    }, { passive: true });
    root.addEventListener('pointerup', cancel);
    root.addEventListener('pointercancel', cancel);
    root.addEventListener('lostpointercapture', cancel);
    return cancel;
  }

  return Object.freeze({ holdDurationMs, movementThresholdPx, isInteractiveTarget, shouldBegin, previewPosition, install });
});
