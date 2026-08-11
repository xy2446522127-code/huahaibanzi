(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) {
    module.exports = api;
  } else {
    root.HuahaiRetention = api;
  }
})(typeof globalThis === 'undefined' ? this : globalThis, function () {
  const supportedDays = new Set([3, 7, 30]);
  const isProtected = record => Boolean(record.fav || record.pin);
  const normalizeDays = value => {
    const days = Number(value);
    return supportedDays.has(days) ? days : 7;
  };
  const normalizeCountLimit = value => {
    const count = Number(value);
    return Number.isInteger(count) && count >= 1 && count <= 10000 ? count : null;
  };

  return {
    normalizeDays,
    normalizeCountLimit,
    prune(records, value) {
      const days = normalizeDays(value);
      return records.filter(record => isProtected(record) || Number(record.ageDays || 0) <= days);
    },
    clearOrdinary(records) {
      return records.filter(isProtected);
    },
    clearEverything() {
      return [];
    },
    trimOrdinary(records, value) {
      const limit = normalizeCountLimit(value);
      if (limit === null) return records.slice();
      const ordinary = records
        .filter(record => !isProtected(record))
        .sort((left, right) => Number(right.copiedAt || 0) - Number(left.copiedAt || 0));
      const keep = new Set(ordinary.slice(0, limit));
      return records.filter(record => isProtected(record) || keep.has(record));
    }
  };
});
