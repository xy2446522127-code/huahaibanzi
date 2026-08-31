const assert = require('node:assert/strict');
const { writeFileSync } = require('node:fs');

const port = Number(process.argv[2]);
const screenshotPath = process.argv[3];
if (!Number.isInteger(port) || port <= 0) throw new Error('A valid WebView2 debugging port is required.');

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

async function findPage(predicate) {
  let lastTargets = [];
  for (let attempt = 0; attempt < 100; attempt += 1) {
    lastTargets = await fetch(`http://127.0.0.1:${port}/json`).then(response => response.json());
    const target = lastTargets.find(candidate => candidate.type === 'page' && predicate(String(candidate.url || '')));
    if (target) return target;
    await delay(100);
  }
  throw new Error(`Expected WebView2 target was not available. Seen: ${lastTargets.map(target => target.url).join(', ')}`);
}

async function connect(target) {
  const socket = new WebSocket(target.webSocketDebuggerUrl);
  const pending = new Map();
  let nextId = 1;
  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data);
    const request = pending.get(message.id);
    if (!request) return;
    pending.delete(message.id);
    if (message.error) request.reject(new Error(message.error.message));
    else request.resolve(message.result);
  });
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });
  const command = (method, params = {}) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await command('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text || 'WebView evaluation failed.');
    return result.result.value;
  };
  return { socket, command, evaluate };
}

async function run() {
  const main = await connect(await findPage(url => url.startsWith('https://app.huahai.local/Web/product-shell.html#')));
  try {
    const id = await main.evaluate(`(async () => {
      for (let index = 0; index < 80; index += 1) {
        const row = document.querySelector('.record');
        if (row) {
          row.dispatchEvent(new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2 }));
          return row.dataset.id;
        }
        await new Promise(resolve => setTimeout(resolve, 100));
      }
      throw new Error('No captured history card was available for preview.');
    })()`);
    assert.match(id, /^[0-9a-f-]{36}$/i, 'The main shell must open a real history record.');

    const preview = await connect(await findPage(url => url === 'https://app.huahai.local/Web/product-shell.html?surface=preview'));
    try {
      for (let attempt = 0; attempt < 80; attempt += 1) {
        if (await preview.evaluate(`Boolean(document.querySelector('#previewEditor'))`)) break;
        if (attempt === 79) throw new Error('Preview editor did not render.');
        await delay(100);
      }
      const inspection = await preview.evaluate(`(() => {
        const editor = document.querySelector('#previewEditor');
        editor.value = '甲'.repeat(121) + '\\n' + '乙'.repeat(40);
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        return {
          title: document.querySelector('.preview-title')?.textContent,
          topLeftIconCount: document.querySelectorAll('.preview-header .fox').length,
          metrics: document.querySelector('#previewMetrics')?.textContent,
          editorScrollable: getComputedStyle(editor).overflowY === 'auto' || getComputedStyle(editor).overflow === 'auto',
          copy: Boolean(document.querySelector('#previewCopy')),
          save: Boolean(document.querySelector('#previewSave')),
          hide: Boolean(document.querySelector('#previewHide'))
        };
      })()`);
      assert.equal(inspection.title, '完整内容预览');
      assert.equal(inspection.topLeftIconCount, 0);
      assert.match(inspection.metrics, /162 字符 · 约 6 行/);
      assert.equal(inspection.editorScrollable, true);
      assert.equal(inspection.copy && inspection.save && inspection.hide, true);
      const confirmation = await preview.evaluate(`(async () => {
        document.querySelector('#previewClose').click();
        for (let attempt = 0; attempt < 20; attempt += 1) {
          const dialog = document.querySelector('.preview-confirm');
          if (dialog) return {
            save: Boolean(dialog.querySelector('[data-preview-confirm="save"]')),
            discard: Boolean(dialog.querySelector('[data-preview-confirm="discard"]')),
            cancel: Boolean(dialog.querySelector('[data-preview-confirm="cancel"]'))
          };
          await new Promise(resolve => setTimeout(resolve, 100));
        }
        throw new Error('Dirty preview close did not show a confirmation.');
      })()`);
      assert.equal(confirmation.save && confirmation.discard && confirmation.cancel, true);
      await preview.evaluate(`(() => { document.querySelector('[data-preview-confirm="cancel"]').click(); return true; })()`);
      if (screenshotPath) {
        const capture = await preview.command('Page.captureScreenshot', { format: 'png', fromSurface: true });
        writeFileSync(screenshotPath, Buffer.from(capture.data, 'base64'));
      }
      await preview.evaluate(`(() => { document.querySelector('#previewCopy').click(); document.querySelector('#previewHide').click(); return true; })()`);
      console.log(JSON.stringify({ passed: 8, inspection, confirmation, screenshotPath: screenshotPath || null }));
    } finally {
      preview.socket.close();
    }
  } finally {
    main.socket.close();
  }
}

run().catch(error => {
  console.error(error.stack || error.message);
  process.exitCode = 1;
});
