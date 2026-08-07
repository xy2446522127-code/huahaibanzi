(function (global, factory) {
  'use strict';
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (global) global.HuahaiShellRouter = api;
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';
  const pages = Object.freeze(['appearance', 'motion', 'input', 'storage', 'system', 'about']);
  const normalizePage = page => pages.includes(page) ? page : 'appearance';
  const settingsHash = page => `#settings/${normalizePage(page)}`;
  const panelHash = () => '#panel';
  function parseHash(hash) {
    const match = String(hash || '').match(/^#settings\/([^/]+)$/);
    if (match && pages.includes(match[1])) return { surface: 'settings', page: match[1] };
    return { surface: 'panel', page: null };
  }
  return Object.freeze({ pages, normalizePage, settingsHash, panelHash, parseHash });
});
