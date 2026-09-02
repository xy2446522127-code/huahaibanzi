(function (global, factory) {
  'use strict';
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (global) global.HuahaiCapsuleGeometry = api;
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';

  const sides = new Set(['left', 'right', 'top']);

  function clamp(value, minimum, maximum) {
    return Math.max(minimum, Math.min(maximum, Number(value) || 0));
  }

  function normalizeCapsuleGeometry(geometry, size, viewport) {
    const side = sides.has(geometry?.side) ? geometry.side : 'left';
    const maximum = side === 'top'
      ? Math.max(0, Number(viewport?.width || 0) - Number(size?.width || 0))
      : Math.max(0, Number(viewport?.height || 0) - Number(size?.height || 0));
    return { side, offset: clamp(geometry?.offset, 0, maximum) };
  }

  function positionForCapsuleSide(geometry, size, viewport) {
    const normalized = normalizeCapsuleGeometry(geometry, size, viewport);
    const width = Math.max(0, Number(viewport?.width || 0) - Number(size?.width || 0));
    if (normalized.side === 'left') return { left: 0, top: normalized.offset };
    if (normalized.side === 'right') return { left: width, top: normalized.offset };
    return { left: normalized.offset, top: 0 };
  }

  function snapCapsuleGeometry(position, size, viewport) {
    const maxLeft = Math.max(0, Number(viewport?.width || 0) - Number(size?.width || 0));
    const maxTop = Math.max(0, Number(viewport?.height || 0) - Number(size?.height || 0));
    const left = clamp(position?.left, 0, maxLeft);
    const top = clamp(position?.top, 0, maxTop);
    const closest = [
      { side: 'left', distance: left, offset: top },
      { side: 'right', distance: maxLeft - left, offset: top },
      { side: 'top', distance: top, offset: left },
    ].sort((first, second) => first.distance - second.distance)[0];
    return normalizeCapsuleGeometry(closest, size, viewport);
  }

  return Object.freeze({ normalizeCapsuleGeometry, positionForCapsuleSide, snapCapsuleGeometry });
});
