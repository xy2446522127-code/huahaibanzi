(function (global, factory) {
  'use strict';
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (global) global.HuahaiInteractionContract = api;
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';

  const staticControls = Object.freeze({
    'panel.search': '#searchInput', 'panel.minimize': '#minimizeButton', 'panel.settings': '#settingsButton', 'panel.summon': '#summonButton', 'records.scroll': '#recordList', 'panel.autohide': '#autoHide', 'panel.drag': '.panel-header',
    'settings.nav.appearance': '.nav-button[data-page="appearance"]', 'settings.nav.motion': '.nav-button[data-page="motion"]', 'settings.nav.input': '.nav-button[data-page="input"]', 'settings.nav.storage': '.nav-button[data-page="storage"]', 'settings.nav.system': '.nav-button[data-page="system"]', 'settings.nav.about': '.nav-button[data-page="about"]', 'settings.back': '#backButton', 'settings.home': '#settingsHome',
    'theme.rose': '.theme[data-theme="rose-purple"]', 'theme.cobalt': '.theme[data-theme="cobalt-blue"]', 'theme.emerald': '.theme[data-theme="emerald-cyan"]', 'theme.amber': '.theme[data-theme="amber-orange"]', 'theme.aurora': '.theme[data-theme="aurora-cyan-purple"]',
    'appearance.opacity': '#opacityRange', 'appearance.scale': '#scaleRange', 'appearance.reset-scale': '#resetScale', 'appearance.resize-handle': '#resizeHandle',
    'motion.petals': '#petalToggle', 'motion.reduced': '#reduceToggle', 'motion.duration': '#durationRange',
    'input.right-double': '#rightDoubleToggle', 'input.capture-shortcut': '#captureShortcut', 'input.reset-shortcut': '#resetShortcut', 'input.exclusions': '#excludeInput', 'input.save-exclusions': '#saveExclude',
    'storage.open-folder': '#openFolder', 'storage.retention-3': '.retention-option[data-days="3"]', 'storage.retention-7': '.retention-option[data-days="7"]', 'storage.retention-30': '.retention-option[data-days="30"]', 'storage.clear-ordinary': '#clearOrdinaryHistory', 'storage.clear-all': '#clearAllHistory',
    'system.startup': '#startupToggle', 'system.background': '#backgroundToggle',
    'about.update-toggle': '#updateAutoToggle', 'about.check-update': '#checkUpdateButton', 'about.install-update': '#installUpdateButton', 'about.open-release': '#releaseButton'
  });
  const filterControls = Object.freeze({ 全部: 'filter.all', 文本: 'filter.text', 链接: 'filter.link', 图片: 'filter.image', 文件: 'filter.file', 收藏: 'filter.favorites' });
  const recordControls = Object.freeze({ row: 'record.copy', pin: 'record.pin', favorite: 'record.favorite', delete: 'record.delete' });
  const exclusionControls = Object.freeze({ remove: 'input.remove-exclusion' });
  const setId = (element, id) => element?.setAttribute('data-apd-control-id', id);

  function markStatic(root = document) {
    Object.entries(staticControls).forEach(([id, selector]) => setId(root.querySelector(selector), id));
  }

  function markFilters(root = document) {
    root.querySelectorAll('.filter').forEach(button => setId(button, filterControls[button.dataset.filter]));
  }

  function markRecord(root = document) {
    root.querySelectorAll('.record').forEach(row => {
      setId(row, recordControls.row);
      setId(row.querySelector('.pin'), recordControls.pin);
      setId(row.querySelector('.fav'), recordControls.favorite);
      setId(row.querySelector('.del'), recordControls.delete);
    });
  }

  function markExclusions(root = document) {
    root.querySelectorAll('.chip button').forEach(button => setId(button, exclusionControls.remove));
  }

  return Object.freeze({ staticControls, filterControls, recordControls, exclusionControls, markStatic, markFilters, markRecord, markExclusions });
});
