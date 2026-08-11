const fs = require('node:fs');
const path = require('node:path');

const port = Number(process.argv[2]);
const outputPath = process.argv[3] ? path.resolve(process.argv[3]) : null;
if (!Number.isInteger(port) || port <= 0) {
  throw new Error('Usage: node HistoryScalePerformanceProbe.cjs <debug-port> [output-path]');
}

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

async function connect() {
  let lastError;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      const targets = await fetch(`http://127.0.0.1:${port}/json`).then(response => response.json());
      const page = targets.find(target =>
        target.type === 'page' && String(target.url || '').startsWith('https://app.huahai.local/Web/product-shell.html'));
      if (page) {
        const socket = new WebSocket(page.webSocketDebuggerUrl);
        const pending = new Map();
        const events = [];
        let nextId = 1;
        socket.addEventListener('message', event => {
          const message = JSON.parse(event.data);
          if (!message.id) {
            events.push(message);
            return;
          }
          const request = pending.get(message.id);
          if (!request) return;
          pending.delete(message.id);
          clearTimeout(request.timer);
          if (message.error) request.reject(new Error(message.error.message));
          else request.resolve(message.result);
        });
        await new Promise((resolve, reject) => {
          socket.addEventListener('open', resolve, { once: true });
          socket.addEventListener('error', reject, { once: true });
        });
        const command = (method, params = {}) => new Promise((resolve, reject) => {
          const id = nextId++;
          const timer = setTimeout(() => {
            pending.delete(id);
            reject(new Error(`CDP command timed out: ${method}`));
          }, 30000);
          pending.set(id, { resolve, reject, timer });
          socket.send(JSON.stringify({ id, method, params }));
        });
        return { socket, command, events, target: page };
      }
    } catch (error) {
      lastError = error;
    }
    await delay(100);
  }
  throw new Error('The Huahai product shell did not expose a CDP target.', { cause: lastError });
}

async function evaluate(command, expression) {
  const response = await command('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  if (response.exceptionDetails) {
    throw new Error(response.exceptionDetails.exception?.description || response.exceptionDetails.text || 'Page evaluation failed.');
  }
  return response.result.value;
}

async function main() {
  const { socket, command, events, target } = await connect();
  try {
    await command('Runtime.enable');
    await command('Log.enable');
    const browserVersion = await command('Browser.getVersion');
    const report = await evaluate(command, `(${async function runProbe() {
      const counts = [100, 500, 1000];
      const repetitions = 27;
      const scrollSampleCount = 20;
      const nextFrame = () => new Promise(resolve => requestAnimationFrame(resolve));
      const settle = async () => { await nextFrame(); await nextFrame(); };
      const percentile = (values, quantile) => {
        const sorted = [...values].sort((left, right) => left - right);
        const index = Math.min(sorted.length - 1, Math.max(0, Math.ceil(sorted.length * quantile) - 1));
        return sorted[index];
      };
      const round = value => Math.round(value * 100) / 100;
      const thumbnail = "data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Crect width='32' height='32' rx='8' fill='%23a95f9e'/%3E%3Cpath d='m3 27 8-10 6 6 5-8 7 12z' fill='%23ffd6ec'/%3E%3C/svg%3E";
      const makeHistory = count => Array.from({ length: count }, (_, index) => ({
        id: `scale-${count}-${index}`,
        kind: index === count - 1 ? '图片' : index % 4 === 1 ? '链接' : index % 4 === 2 ? '文件' : '文本',
        text: `匿名性能记录 ${String(index).padStart(4, '0')}`,
        meta: `测试来源 · ${index}`,
        ageDays: index % 7,
        fav: index % 17 === 0,
        pin: false,
        thumbnailAvailable: index === count - 1,
        thumbnail: index === count - 1 ? thumbnail : null,
      }));
      const baseSettings = {
        retentionDays: 7,
        panelScale: 1,
        opacity: 0.88,
        theme: 'rose-purple',
        petalsEnabled: false,
        reduceMotion: true,
        rightDoubleClick: true,
        startupEnabled: false,
        backgroundEnabled: true,
        hideOnOutsideClick: true,
        checkUpdatesOnStartup: false,
        exclusions: [],
        dataPath: 'isolated-performance-fixture',
      };
      const list = document.querySelector('#recordList');
      const autoHide = document.querySelector('#autoHide');
      if (!list || typeof window.HuahaiApplyNativeState !== 'function') throw new Error('History performance surface is unavailable.');
      if (autoHide) autoHide.checked = false;
      const results = [];
      for (const count of counts) {
        const history = makeHistory(count);
        const durations = [];
        for (let repetition = 0; repetition < repetitions; repetition += 1) {
          list.scrollTop = 0;
          const started = performance.now();
          window.HuahaiApplyNativeState({ history, settings: baseSettings });
          await settle();
          durations.push(performance.now() - started);
        }
        const measured = durations.slice(2);
        results.push({
          count,
          p50: round(percentile(measured, 0.5)),
          p95: round(percentile(measured, 0.95)),
          max: round(Math.max(...measured)),
          domRows: document.querySelectorAll('.record').length,
          scrollHeight: list.scrollHeight,
          viewportHeight: list.clientHeight,
        });
      }

      const maxScroll = Math.max(0, list.scrollHeight - list.clientHeight);
      const scrollSamples = [];
      let previousFirstIndex = -1;
      let blankSamples = 0;
      let duplicateSamples = 0;
      let rollbackSamples = 0;
      for (let sampleIndex = 0; sampleIndex < scrollSampleCount; sampleIndex += 1) {
        const requested = maxScroll * sampleIndex / (scrollSampleCount - 1);
        list.scrollTop = requested;
        list.dispatchEvent(new Event('scroll'));
        await settle();
        const rows = [...document.querySelectorAll('.record')];
        const ids = rows.map(row => row.dataset.id);
        const firstIndex = Number(ids[0]?.split('-').at(-1));
        if (rows.length === 0) blankSamples += 1;
        if (new Set(ids).size !== ids.length) duplicateSamples += 1;
        if (Number.isFinite(firstIndex) && firstIndex < previousFirstIndex) rollbackSamples += 1;
        if (Number.isFinite(firstIndex)) previousFirstIndex = firstIndex;
        scrollSamples.push({
          sample: sampleIndex,
          requested: round(requested),
          actual: round(list.scrollTop),
          firstId: ids[0] || null,
          lastId: ids.at(-1) || null,
          domRows: rows.length,
        });
      }
      const terminalId = 'scale-1000-999';
      const terminalRow = document.querySelector(`.record[data-id="${terminalId}"]`);
      const finalRecordVisible = Boolean(terminalRow);
      const thumbnailVisible = Boolean(terminalRow?.querySelector('.record-thumbnail[src^="data:image/"]'));
      const searchInput = document.querySelector('#searchInput');
      searchInput.value = '0999';
      searchInput.dispatchEvent(new Event('input', { bubbles: true }));
      await settle();
      const searchedRows = [...document.querySelectorAll('.record')];
      const search = {
        query: '0999',
        matchedIds: searchedRows.map(row => row.dataset.id),
        passed: searchedRows.length === 1 && searchedRows[0].dataset.id === terminalId,
      };

      searchInput.value = '';
      searchInput.dispatchEvent(new Event('input', { bubbles: true }));
      document.querySelector('.filter[data-filter="图片"]')?.click();
      await settle();
      const filteredRows = [...document.querySelectorAll('.record')];
      const filter = {
        name: '图片',
        matchedIds: filteredRows.map(row => row.dataset.id),
        passed: filteredRows.length === 1 && filteredRows[0].dataset.id === terminalId,
      };

      document.querySelector('.filter[data-filter="全部"]')?.click();
      await settle();
      const actionTargetId = 'scale-1000-840';
      list.scrollTop = scrollSamples[16].actual;
      list.dispatchEvent(new Event('scroll'));
      await settle();
      const actionRow = document.querySelector(`.record[data-id="${actionTargetId}"]`);
      const nativeBridge = window.chrome?.webview;
      const originalPostMessage = nativeBridge?.postMessage;
      const emitted = [];
      let interceptionAvailable = false;
      if (nativeBridge && typeof originalPostMessage === 'function') {
        try {
          nativeBridge.postMessage = message => emitted.push(JSON.parse(JSON.stringify(message)));
          interceptionAvailable = nativeBridge.postMessage !== originalPostMessage;
        } catch { }
      }
      try {
        const autoHide = document.querySelector('#autoHide');
        if (autoHide) autoHide.checked = false;
        actionRow?.click();
        actionRow?.querySelector('.pin')?.click();
        actionRow?.querySelector('.fav')?.click();
        actionRow?.querySelector('.del')?.click();
        await settle();
      } finally {
        if (interceptionAvailable) nativeBridge.postMessage = originalPostMessage;
      }
      const expectedActions = ['copy', 'togglePin', 'toggleFavorite', 'delete'];
      const delegatedMessages = emitted.filter(message => expectedActions.includes(message.action));
      const initialFirstIndex = Number(scrollSamples[0].firstId?.split('-').at(-1));
      const initialLastIndex = Number(scrollSamples[0].lastId?.split('-').at(-1));
      const actionTargetIndex = Number(actionTargetId.split('-').at(-1));
      const delegatedActions = {
        targetId: actionTargetId,
        outsideInitialWindow: actionTargetIndex < initialFirstIndex || actionTargetIndex > initialLastIndex,
        interceptionAvailable,
        emitted: delegatedMessages,
        passed: Boolean(actionRow) && interceptionAvailable && expectedActions.every(action =>
          delegatedMessages.some(message => message.action === action && String(message.id) === actionTargetId)),
      };
      const maxDomRows = Math.max(...results.map(result => result.domRows), ...scrollSamples.map(sample => sample.domRows));
      const failures = [];
      if (maxDomRows > 80) failures.push(`virtual DOM row budget exceeded: ${maxDomRows}`);
      for (const result of results) if (result.p95 > 50) failures.push(`${result.count} record P95 exceeded 50 ms: ${result.p95}`);
      if (blankSamples) failures.push(`blank scroll samples: ${blankSamples}`);
      if (duplicateSamples) failures.push(`duplicate scroll samples: ${duplicateSamples}`);
      if (rollbackSamples) failures.push(`rollback scroll samples: ${rollbackSamples}`);
      if (!finalRecordVisible) failures.push('terminal record was not rendered at the final scroll position');
      if (!thumbnailVisible) failures.push('terminal image thumbnail was not rendered');
      if (!search.passed) failures.push(`search journey failed: ${JSON.stringify(search)}`);
      if (!filter.passed) failures.push(`filter journey failed: ${JSON.stringify(filter)}`);
      if (!delegatedActions.passed) failures.push(`delegated action journey failed: ${JSON.stringify(delegatedActions)}`);
      return {
        version: 2,
        target: 'installed-webview2',
        thresholds: { maxDomRows: 80, p95Milliseconds: 50, scrollSamples: scrollSampleCount },
        results,
        scroll: { samples: scrollSamples, blankSamples, duplicateSamples, rollbackSamples, finalRecordVisible, thumbnailVisible },
        journeys: { search, filter, delegatedActions },
        failures,
      };
    }.toString()})()`);
    report.identity = {
      executablePath: process.env.HUAHAI_HISTORY_EXE_PATH || null,
      executableSha256: process.env.HUAHAI_HISTORY_EXE_SHA256 || null,
      executableVersion: process.env.HUAHAI_HISTORY_EXE_VERSION || null,
      sourceRevision: process.env.HUAHAI_HISTORY_SOURCE_REVISION || null,
      sourceDirty: process.env.HUAHAI_HISTORY_SOURCE_DIRTY === 'true',
      sourceShellSha256: process.env.HUAHAI_HISTORY_SOURCE_SHELL_SHA256 || null,
      sourceVirtualListSha256: process.env.HUAHAI_HISTORY_SOURCE_VIRTUAL_SHA256 || null,
      packagedShellSha256: process.env.HUAHAI_HISTORY_PACKAGED_SHELL_SHA256 || null,
      packagedVirtualListSha256: process.env.HUAHAI_HISTORY_PACKAGED_VIRTUAL_SHA256 || null,
      targetUrl: target.url,
      targetTitle: target.title,
      browserProduct: browserVersion.product,
      browserProtocolVersion: browserVersion.protocolVersion,
      browserUserAgent: browserVersion.userAgent,
      browserJsVersion: browserVersion.jsVersion,
    };
    if (!report.identity.executableSha256 || !report.identity.sourceRevision || !report.identity.browserProduct) {
      report.failures.push('candidate identity is incomplete');
    }
    if (report.identity.sourceShellSha256 !== report.identity.packagedShellSha256 ||
        report.identity.sourceVirtualListSha256 !== report.identity.packagedVirtualListSha256) {
      report.failures.push('packaged UI assets do not match the tested source');
    }
    const consoleErrors = events.filter(event =>
      event.method === 'Runtime.exceptionThrown' ||
      event.method === 'Log.entryAdded' && event.params?.entry?.level === 'error' ||
      event.method === 'Runtime.consoleAPICalled' && event.params?.type === 'error')
      .map(event => JSON.stringify(event.params).slice(0, 500));
    report.consoleErrors = consoleErrors;
    if (consoleErrors.length) report.failures.push(`console errors: ${consoleErrors.length}`);
    if (outputPath) {
      fs.mkdirSync(path.dirname(outputPath), { recursive: true });
      fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
    }
    process.stdout.write(JSON.stringify(report));
    if (report.failures.length) process.exitCode = 1;
  } finally {
    socket.close();
  }
}

main().catch(error => {
  console.error(error.stack || error.message);
  process.exitCode = 1;
});
