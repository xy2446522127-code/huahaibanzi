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

  return {
    normalizeDays,
    prune(records, value) {
      const days = normalizeDays(value);
      return records.filter(record => isProtected(record) || Number(record.ageDays || 0) <= days);
    },
    clearOrdinary(records) {
      return records.filter(isProtected);
    },
    clearEverything() {
      return [];
    }
  };
});
