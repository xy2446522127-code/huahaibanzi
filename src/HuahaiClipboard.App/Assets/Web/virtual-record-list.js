(function (global, factory) {
  'use strict';
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (global) global.HuahaiVirtualRecordList = api;
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';

  function calculateWindow({
    itemCount,
    scrollTop,
    viewportHeight,
    rowExtent,
    overscan = 4,
  }) {
    const count = Math.max(0, Math.trunc(Number(itemCount) || 0));
    if (count === 0) {
      return { start: 0, end: 0, topSpacer: 0, bottomSpacer: 0 };
    }

    const extent = Math.max(1, Number(rowExtent) || 1);
    const safeOverscan = Math.max(0, Math.trunc(Number(overscan) || 0));
    const first = Math.floor(Math.max(0, Number(scrollTop) || 0) / extent);
    const visible = Math.max(1, Math.ceil(Math.max(0, Number(viewportHeight) || 0) / extent));
    const start = Math.max(0, Math.min(count - 1, first - safeOverscan));
    const end = Math.min(count, Math.max(start + 1, first + visible + safeOverscan));

    return {
      start,
      end,
      topSpacer: start * extent,
      bottomSpacer: (count - end) * extent,
    };
  }

  function createFrameScheduler({ scheduleFrame, cancelFrame, render }) {
    if (typeof scheduleFrame !== 'function' || typeof render !== 'function') {
      throw new TypeError('scheduleFrame and render are required');
    }

    let pendingValue;
    let frameHandle = null;
    let disposed = false;

    const flush = () => {
      if (disposed) return;
      if (frameHandle !== null && typeof cancelFrame === 'function') {
        cancelFrame(frameHandle);
      }
      frameHandle = null;
      if (pendingValue === undefined) return;
      const value = pendingValue;
      pendingValue = undefined;
      render(value);
    };

    const request = value => {
      if (disposed) return;
      pendingValue = value;
      if (frameHandle !== null) return;
      frameHandle = scheduleFrame(() => {
        frameHandle = null;
        if (pendingValue === undefined || disposed) return;
        const latest = pendingValue;
        pendingValue = undefined;
        render(latest);
      });
    };

    const dispose = () => {
      if (disposed) return;
      disposed = true;
      if (frameHandle !== null && typeof cancelFrame === 'function') {
        cancelFrame(frameHandle);
      }
      frameHandle = null;
      pendingValue = undefined;
    };

    return Object.freeze({ request, flush, dispose });
  }

  return Object.freeze({ calculateWindow, createFrameScheduler });
});
