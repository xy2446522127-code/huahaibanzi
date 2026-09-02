(function (global, factory) {
  'use strict';
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  if (global) global.HuahaiPreviewPrototype = api;
})(typeof window !== 'undefined' ? window : globalThis, function () {
  'use strict';

  const minimumWidth = 420;
  const minimumHeight = 360;

  function createState(record, overrides = {}) {
    const text = String(record?.text ?? '');
    const note = String(record?.note ?? record?.remark ?? '');
    return {
      recordId: String(record?.id ?? ''),
      kind: String(record?.kind ?? '文本'),
      savedDraft: text,
      draft: text,
      savedNote: note,
      noteDraft: note,
      dirty: false,
      noteEditing: false,
      visible: true,
      topmost: record?.pin === true,
      autoHide: true,
      ...overrides,
    };
  }

  function withDirty(state, changes) {
    const next = { ...state, ...changes };
    next.dirty = next.draft !== next.savedDraft || next.noteDraft !== next.savedNote;
    return next;
  }

  function updateDraft(state, value) {
    return withDirty(state, { draft: String(value ?? '') });
  }

  function updateNoteDraft(state, value) {
    return withDirty(state, { noteDraft: String(value ?? '') });
  }

  function setRecordPin(records, recordId, pinned) {
    const id = String(recordId ?? '');
    return (Array.isArray(records) ? records : []).map(record =>
      String(record?.id ?? '') === id ? { ...record, pin: pinned === true } : record,
    );
  }

  function save(state, records) {
    const source = Array.isArray(records) ? records : [];
    const index = source.findIndex(record => String(record.id) === state.recordId);
    if (index < 0) return { ok: false, error: '原记录已不存在', state, records: source };

    const value = String(state.draft ?? '').trim();
    if (!value) {
      const label = state.kind === '图片' || state.kind === '文件' ? '显示名称不能为空' : '内容不能为空';
      return { ok: false, error: label, state, records: source };
    }

    const note = String(state.noteDraft ?? '').trim();
    const nextRecords = source.map((record, recordIndex) => {
      if (recordIndex !== index) return record;
      const updated = { ...record, text: value };
      if (Object.prototype.hasOwnProperty.call(record, 'remark')) updated.remark = note;
      else updated.note = note;
      if (updated.kind === '链接') {
        try {
          const url = new URL(value);
          if (url.protocol !== 'http:' && url.protocol !== 'https:') updated.kind = '文本';
        } catch {
          updated.kind = '文本';
        }
      }
      return updated;
    });
    const nextState = {
      ...state,
      kind: nextRecords[index].kind,
      savedDraft: value,
      draft: value,
      savedNote: note,
      noteDraft: note,
      dirty: false,
      noteEditing: false,
      visible: false,
    };
    return { ok: true, state: nextState, records: nextRecords };
  }

  function clampGeometry(geometry, viewport) {
    const viewportWidth = Math.max(minimumWidth, Number(viewport?.width) || minimumWidth);
    const viewportHeight = Math.max(minimumHeight, Number(viewport?.height) || minimumHeight);
    const width = Math.min(viewportWidth, Math.max(minimumWidth, Number(geometry?.width) || 650));
    const height = Math.min(viewportHeight, Math.max(minimumHeight, Number(geometry?.height) || 500));
    const left = Math.max(0, Math.min(viewportWidth - width, Number(geometry?.left) || 0));
    const top = Math.max(0, Math.min(viewportHeight - height, Number(geometry?.top) || 0));
    return { left, top, width, height };
  }

  function moveGeometry(start, delta, viewport) {
    return clampGeometry({
      ...start,
      left: Number(start?.left || 0) + Number(delta?.x || 0),
      top: Number(start?.top || 0) + Number(delta?.y || 0),
    }, viewport);
  }

  function resizeGeometry(start, delta, viewport) {
    const viewportWidth = Math.max(minimumWidth, Number(viewport?.width) || minimumWidth);
    const viewportHeight = Math.max(minimumHeight, Number(viewport?.height) || minimumHeight);
    const left = Math.max(0, Number(start?.left) || 0);
    const top = Math.max(0, Number(start?.top) || 0);
    return {
      left,
      top,
      width: Math.min(viewportWidth - left, Math.max(minimumWidth, Number(start?.width || 650) + Number(delta?.x || 0))),
      height: Math.min(viewportHeight - top, Math.max(minimumHeight, Number(start?.height || 500) + Number(delta?.y || 0))),
    };
  }

  return Object.freeze({
    minimumWidth,
    minimumHeight,
    createState,
    updateDraft,
    updateNoteDraft,
    setRecordPin,
    save,
    clampGeometry,
    moveGeometry,
    resizeGeometry,
  });
});
