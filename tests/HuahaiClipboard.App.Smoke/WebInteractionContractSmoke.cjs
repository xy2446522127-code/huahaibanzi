const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

const port = Number(process.argv[2]);
const contractPath = process.argv[3];
const shellPath = process.argv[4];
const outputPath = process.argv[5];
if (!Number.isInteger(port) || !contractPath || !shellPath || !outputPath) {
  throw new Error('Usage: node WebInteractionContractSmoke.cjs <port> <contract> <shell> <output>');
}

const interaction = require(path.join(path.dirname(shellPath), 'interaction-contract.js'));
const expectedRuntimeControlIds = [...new Set([
  ...Object.keys(interaction.staticControls),
  ...Object.values(interaction.filterControls),
  ...Object.values(interaction.recordControls),
  ...Object.values(interaction.exclusionControls),
])];

const delay = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

async function connect() {
  const targets = await fetch(`http://127.0.0.1:${port}/json`).then(response => response.json());
  const page = targets.find(target => target.type === 'page');
  if (!page) throw new Error('No Edge page target was found.');
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
    }, 15000);
    pending.set(id, { resolve, reject, timer });
    socket.send(JSON.stringify({ id, method, params }));
  });
  return { socket, command, events };
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

async function waitFor(command, expression, label) {
  for (let attempt = 0; attempt < 100; attempt += 1) {
    if (await evaluate(command, expression)) return;
    await delay(50);
  }
  throw new Error(`${label} timed out.`);
}

function interactionExpression(controlId) {
  return `(${async function runControl(id) {
    const byId = value => document.querySelector('[data-apd-control-id="' + value + '"]');
    const rows = () => [...document.querySelectorAll('.record')];
    const kinds = () => rows().map(row => row.querySelector('.kind')?.title);
    const pause = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
    const click = value => { const element = byId(value); if (!element) throw new Error('missing ' + value); element.click(); return element; };
    const input = (value, next) => { const element = byId(value); element.value = String(next); element.dispatchEvent(new Event('input', { bubbles: true })); return element; };
    const toggleChanged = value => { const element = byId(value); const before = element.classList.contains('on'); element.click(); return element.classList.contains('on') !== before; };

    if (id === 'panel.search') { input(id, 'reactbits'); return rows().length === 1 && rows()[0].textContent.includes('reactbits'); }
    if (id === 'panel.minimize') { click(id); return document.querySelector('#glassPanel').classList.contains('hidden') && document.querySelector('#launcher').classList.contains('show'); }
    if (id === 'panel.summon') { document.querySelector('#minimizeButton').click(); click(id); return !document.querySelector('#glassPanel').classList.contains('hidden') && document.activeElement === document.querySelector('#searchInput'); }
    if (id === 'panel.settings') { click(id); await pause(0); return document.querySelector('#glassPanel').classList.contains('settings-mode') && location.hash === '#settings/appearance'; }
    if (id === 'panel.update-later') { const banner=document.querySelector('#updateBanner');banner.hidden=false;click(id);return banner.hidden&&!document.querySelector('#glassPanel').classList.contains('settings-mode'); }
    if (id === 'panel.update-install') { const banner=document.querySelector('#updateBanner');banner.hidden=false;click(id);await pause(0);return banner.hidden&&document.querySelector('#glassPanel').classList.contains('settings-mode')&&location.hash==='#settings/about'; }
    const filterKinds = { 'filter.text': '文本', 'filter.link': '链接', 'filter.image': '图片', 'filter.file': '文件' };
    if (id === 'filter.all') { click(id); return rows().length === 12; }
    if (filterKinds[id]) { click(id); return rows().length > 0 && kinds().every(kind => kind === filterKinds[id]); }
    if (id === 'filter.favorites') { click(id); return rows().length > 0 && rows().length < 12; }
    if (id === 'records.scroll') { const list = byId(id); list.scrollTop = Math.min(180, list.scrollHeight); list.dispatchEvent(new WheelEvent('wheel', { bubbles: true, deltaY: 180 })); return getComputedStyle(list).overflowY === 'auto' && list.scrollHeight > list.clientHeight && list.scrollTop > 0; }
    if (id === 'record.copy') { click(id); return document.querySelector('#glassPanel').classList.contains('hidden'); }
    if (id === 'record.pin' || id === 'record.favorite') { const row = byId('record.copy'); const recordId = row.dataset.id; const selector = id === 'record.pin' ? '.pin' : '.fav'; const before = row.querySelector(selector).classList.contains('on'); click(id); const after = document.querySelector('.record[data-id="' + recordId + '"]'); return Boolean(after) && after.querySelector(selector).classList.contains('on') !== before; }
    if (id === 'record.delete') { const before = rows().length; click(id); return rows().length === before - 1; }
    if (id === 'panel.autohide') { const element = byId(id); const before = element.checked; element.click(); return element.checked !== before; }
    if (id === 'panel.drag') { const panel = document.querySelector('#glassPanel'); const before = panel.getBoundingClientRect(); panel.setPointerCapture = () => {}; panel.releasePointerCapture = () => {}; const element = byId(id); element.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, pointerId: 7, clientX: before.left + 80, clientY: before.top + 20 })); document.dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerId: 7, clientX: before.left + 20, clientY: before.top + 80 })); document.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerId: 7, clientX: before.left + 20, clientY: before.top + 80 })); const after = panel.getBoundingClientRect(); return Math.abs(after.left - before.left) > 10 || Math.abs(after.top - before.top) > 10; }

    if (id.startsWith('settings.nav.')) { const page = id.split('.').at(-1); click(id); await pause(0); return document.querySelector('.settings-page.active')?.dataset.page === page && location.hash === '#settings/' + page; }
    if (id === 'settings.back' || id === 'settings.home') { click(id); await pause(0); return !document.querySelector('#glassPanel').classList.contains('settings-mode') && location.hash === '#panel'; }
    if (id.startsWith('theme.')) { click(id); return byId(id).classList.contains('active') && Boolean(getComputedStyle(document.documentElement).getPropertyValue('--accent').trim()); }
    if (id === 'appearance.opacity') { input(id, 70); return document.querySelector('#opacityValue').textContent === '70%' && Number(getComputedStyle(document.documentElement).getPropertyValue('--glass-material-opacity')) === 0.7; }
    if (id === 'appearance.scale') { input(id, 117); await pause(20); return document.querySelector('#scaleValue').textContent === '117%' && Number(getComputedStyle(document.documentElement).getPropertyValue('--panel-scale')) === 1.17; }
    if (id === 'appearance.reset-scale') { input('appearance.scale', 120); click(id); return document.querySelector('#scaleValue').textContent === '100%'; }
    if (id === 'appearance.resize-handle') { const handle=byId(id);const before=document.querySelector('#scaleValue').textContent;handle.setPointerCapture=()=>{};handle.releasePointerCapture=()=>{};handle.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true,pointerId:11,clientX:400,clientY:640}));handle.dispatchEvent(new PointerEvent('pointermove',{bubbles:true,pointerId:11,clientX:450,clientY:690}));handle.dispatchEvent(new PointerEvent('pointerup',{bubbles:true,pointerId:11,clientX:450,clientY:690}));return document.querySelector('#scaleValue').textContent!==before; }
    if (id === 'motion.petals') { const changed = toggleChanged(id); return changed && document.querySelector('#petals').classList.contains('off') === !byId(id).classList.contains('on'); }
    if (id === 'motion.reduced') { const changed = toggleChanged(id); return changed && document.querySelector('#desktop').classList.contains('reduced') === byId(id).classList.contains('on'); }
    if (id === 'motion.duration') { input(id, 700); return document.querySelector('#durationValue').textContent === '700ms'; }
    if (id === 'input.right-double') return toggleChanged(id);
    if (id === 'input.capture-shortcut') { click(id); document.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'k', ctrlKey: true })); return document.querySelector('#shortcutValue').textContent.includes('K') && !byId(id).classList.contains('listening'); }
    if (id === 'input.reset-shortcut') { click(id); return document.querySelector('#shortcutValue').textContent === '未设置'; }
    if (id === 'input.exclusions') { input(id, 'Example.exe'); return byId(id).value === 'Example.exe'; }
    if (id === 'input.save-exclusions') { input('input.exclusions', 'Example.exe\nAnother.exe'); click(id); return document.querySelectorAll('#excludeChips .chip').length === 2; }
    if (id === 'input.remove-exclusion') { const before=document.querySelectorAll('#excludeChips .chip').length;click(id);return before>0&&document.querySelectorAll('#excludeChips .chip').length===before-1; }
    if (id === 'storage.open-folder') { click(id); return document.querySelector('#toast').classList.contains('show') && document.querySelector('#toast').textContent.includes('EXE'); }
    if (id.startsWith('storage.retention-')) { click(id); return byId(id).classList.contains('active'); }
    if (id === 'storage.clear-ordinary') { const before = rows().length; click(id); return rows().length > 0 && rows().length < before; }
    if (id === 'storage.clear-all') { click(id); click(id); return rows().length === 0; }
    if (id === 'system.startup' || id === 'system.background' || id === 'system.outside-hide' || id === 'about.update-toggle') return toggleChanged(id);
    if (id === 'about.check-update') { click(id); await pause(750); return document.querySelector('#updateStatus').classList.contains('available') && !document.querySelector('#installUpdateButton').hidden; }
    if (id === 'about.install-update') { document.querySelector('#checkUpdateButton').click(); await pause(750); click(id); await pause(1000); return document.querySelector('#updateProgress').getAttribute('aria-valuenow') === '100' && document.querySelector('#updateStatus').textContent.includes('演示完成'); }
    if (id === 'about.snooze-update') { document.querySelector('#checkUpdateButton').click(); await pause(750); click(id); return document.querySelector('#toast').classList.contains('show') && document.querySelector('#toast').textContent.includes('24 小时'); }
    if (id === 'about.open-release') { click(id); return document.querySelector('#toast').classList.contains('show') && document.querySelector('#toast').textContent.includes('GitHub Release'); }
    throw new Error('unsupported web interaction ' + id);
  }.toString()})(${JSON.stringify(controlId)})`;
}

async function main() {
  const contract = JSON.parse(fs.readFileSync(contractPath, 'utf8'));
  const controls = contract.controls.filter(control => control.fixture.route.startsWith('https://app.huahai.local/Web/product-shell.html'));
  const declared = new Set(controls.map(control => control.control_id));
  const baseUrl = pathToFileURL(shellPath).href;
  const { socket, command, events } = await connect();
  const results = [];
  const runtimeIds = new Set();
  const unexplainedRuntimeControls = new Set();
  try {
    await command('Runtime.enable');
    await command('Log.enable');
    for (const [controlIndex, control] of controls.entries()) {
      const hash = new URL(control.fixture.route).hash;
      const fixtureUrl = new URL(baseUrl);
      fixtureUrl.searchParams.set('apd-fixture', String(controlIndex));
      fixtureUrl.hash = hash;
      await command('Page.navigate', { url: fixtureUrl.href });
      await waitFor(command, `document.readyState==='complete'&&${JSON.stringify(expectedRuntimeControlIds)}.every(id=>document.querySelector('[data-apd-control-id="'+id+'"]'))`, `${control.control_id} route`);
      const discovered = await evaluate(command, `[...document.querySelectorAll('[data-apd-control-id]')].map(element=>element.getAttribute('data-apd-control-id'))`);
      discovered.forEach(id => runtimeIds.add(id));
      const unexplained = await evaluate(command, `(() => {
        const nativeTags=new Set(['BUTTON','INPUT','SELECT','TEXTAREA','A']);
        return [...document.querySelectorAll('.experience *')]
          .filter(element=>nativeTags.has(element.tagName)||(element.getAttribute('role')&&['button','slider','checkbox','link','menuitem'].includes(element.getAttribute('role')))||element.hasAttribute('tabindex')||typeof element.onclick==='function'||typeof element.onpointerdown==='function')
          .filter(element=>!element.hasAttribute('disabled'))
          .filter(element=>!element.getAttribute('data-apd-control-id'))
          .map(element=>element.id||element.className||element.tagName)
      })()`);
      unexplained.forEach(id => unexplainedRuntimeControls.add(String(id)));
      const count = await evaluate(command, `document.querySelectorAll('[data-apd-control-id=${JSON.stringify(control.control_id)}]').length`);
      if (count < 1) {
        results.push({ control_id: control.control_id, status: 'missing', reached: false, triggered: false, matched: false, behavior: control.targets.web.behavior });
        continue;
      }
      try {
        const matched = Boolean(await evaluate(command, interactionExpression(control.control_id)));
        results.push({ control_id: control.control_id, status: matched ? 'passed' : 'failed', reached: true, triggered: true, matched, behavior: control.targets.web.behavior });
      } catch (error) {
        results.push({ control_id: control.control_id, status: 'failed', reached: true, triggered: false, matched: false, behavior: control.targets.web.behavior, error: String(error.message).slice(0, 500) });
      }
    }
  } finally {
    await command('Browser.close').catch(() => {});
    socket.close();
  }

  const consoleErrors = events.filter(event =>
    event.method === 'Runtime.exceptionThrown' ||
    event.method === 'Log.entryAdded' && event.params?.entry?.level === 'error' ||
    event.method === 'Runtime.consoleAPICalled' && event.params?.type === 'error'
  ).map(event => JSON.stringify(event.params).slice(0, 500));
  const report = {
    version: 1,
    target: 'web',
    contract_revision: contract.contract_revision,
    results,
    unexplained_controls: [...new Set([...runtimeIds].filter(id => !declared.has(id)).concat([...unexplainedRuntimeControls]))],
    console_errors: consoleErrors,
    runner: 'tests/HuahaiClipboard.App.Smoke/WebInteractionContractSmoke.cjs',
  };
  fs.mkdirSync(require('node:path').dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`, 'utf8');
  const failed = results.filter(result => result.status !== 'passed');
  process.stdout.write(JSON.stringify({ controls: results.length, passed: results.length - failed.length, failed, unexplained: report.unexplained_controls.length, consoleErrors: consoleErrors.length }));
  if (failed.length || report.unexplained_controls.length || consoleErrors.length) process.exitCode = 1;
}

main().catch(error => {
  console.error(error.stack || error.message);
  process.exitCode = 1;
});
