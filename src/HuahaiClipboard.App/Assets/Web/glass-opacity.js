(function (global, factory) {
  'use strict';

  const api = factory();
  if (typeof module === 'object' && module.exports) {
    module.exports = api;
  }
  if (global) {
    global.HuahaiGlassOpacity = api;
  }
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';

  function apply(style, value) {
    const numeric = Number(value);
    const fraction = numeric > 1 ? numeric / 100 : numeric;
    const normalized = Math.min(0.96, Math.max(0.65, Number.isFinite(fraction) ? fraction : 0.88));
    const percentage = `${Math.round(normalized * 100)}%`;

    style.setProperty('--glass-material-opacity', String(normalized));
    style.setProperty('--glass-material-opacity-percent', percentage);
    return normalized;
  }

  return Object.freeze({ apply });
});
