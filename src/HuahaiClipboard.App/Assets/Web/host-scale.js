(function (global) {
  'use strict';

  global.HuahaiHostScale = Object.freeze({
    isNativeShellHost(locationLike, chromeLike) {
      return Boolean(
        locationLike &&
        String(locationLike.hostname || '').toLowerCase() === 'app.huahai.local' &&
        chromeLike &&
        chromeLike.webview &&
        typeof chromeLike.webview.postMessage === 'function'
      );
    },
    zoomForDevicePixelRatio(value) {
      const ratio = Number(value);
      return Number.isFinite(ratio) && ratio > 0 ? 1 / ratio : 1;
    },
    layoutPixels(viewportPixels, zoom) {
      const safeZoom = Number.isFinite(zoom) && zoom > 0 ? zoom : 1;
      return Math.round(Number(viewportPixels) / safeZoom);
    },
    clampPanelScale(value) {
      const scale = Number(value);
      if (!Number.isFinite(scale)) {
        return 1;
      }
      return Math.max(0.8, Math.min(1.6, scale));
    }
  });
})(window);
