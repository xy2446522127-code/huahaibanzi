(function (global) {
  'use strict';

  const MINIMUM = 80;
  const MAXIMUM = 160;
  const DEFAULT = 100;

  function normalizePercent(value) {
    const numeric = Number(value);
    if (!Number.isFinite(numeric)) return DEFAULT;
    return Math.max(MINIMUM, Math.min(MAXIMUM, Math.round(numeric)));
  }

  function toRatio(percent) {
    return normalizePercent(percent) / 100;
  }

  function createController(options) {
    const scheduleFrame = options.scheduleFrame;
    const cancelFrame = options.cancelFrame;
    let committedPercent = DEFAULT;
    let previewPercent = DEFAULT;
    let pendingPercent = null;
    let frameId = 0;

    function renderPreview(percent) {
      previewPercent = normalizePercent(percent);
      options.render(previewPercent);
      options.preview(toRatio(previewPercent));
    }

    return Object.freeze({
      setCommitted(value) {
        committedPercent = normalizePercent(value);
        previewPercent = committedPercent;
        pendingPercent = null;
        if (frameId) cancelFrame(frameId);
        frameId = 0;
        options.render(committedPercent);
      },
      preview(value) {
        pendingPercent = normalizePercent(value);
        if (frameId) return;
        frameId = scheduleFrame(() => {
          frameId = 0;
          const next = pendingPercent;
          pendingPercent = null;
          renderPreview(next);
        });
      },
      commit(value) {
        if (frameId) cancelFrame(frameId);
        frameId = 0;
        pendingPercent = null;
        committedPercent = normalizePercent(value);
        previewPercent = committedPercent;
        options.render(committedPercent);
        options.commit(toRatio(committedPercent));
      },
      cancel() {
        if (frameId) cancelFrame(frameId);
        frameId = 0;
        pendingPercent = null;
        previewPercent = committedPercent;
        options.render(committedPercent);
        options.preview(toRatio(committedPercent));
      },
      currentPercent() { return previewPercent; },
      committedPercent() { return committedPercent; },
    });
  }

  global.HuahaiPanelScale = Object.freeze({
    minimumPercent: MINIMUM,
    maximumPercent: MAXIMUM,
    defaultPercent: DEFAULT,
    normalizePercent,
    toRatio,
    createController,
  });
})(window);
