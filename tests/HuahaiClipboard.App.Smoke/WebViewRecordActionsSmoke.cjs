const { writeFileSync } = require('node:fs');
const { resolve } = require('node:path');

const port = Number(process.argv[2]);
const operation = process.argv[3];
const value = process.argv[4] || '';
if (!Number.isInteger(port) || port <= 0) throw new Error('A valid WebView2 debugging port is required.');

async function connect() {
  const targets = await fetch(`http://127.0.0.1:${port}/json`).then(response => response.json());
  const page = targets.find(target => target.type === 'page');
  if (!page) throw new Error('No WebView2 page target was found.');
  const socket = new WebSocket(page.webSocketDebuggerUrl);
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
  return { socket, command };
}

async function run() {
  const { socket, command } = await connect();
  if (operation === 'capture') {
    const screenshot = await command('Page.captureScreenshot', { format: 'png', fromSurface: true });
    const output = resolve(value);
    writeFileSync(output, Buffer.from(screenshot.data, 'base64'));
    socket.close();
    console.log(JSON.stringify({ captured: true, output }));
    return;
  }
  const quoted = JSON.stringify(value);
  const expression = operation === 'open-settings'
    ? `(async () => {
        const button=document.querySelector('#settingsButton');
        if (!button) throw new Error('settings button missing');
        button.click();
        for (let i=0;i<50;i++) {
          if (document.querySelector('#glassPanel')?.classList.contains('settings-mode')) return { opened: true };
          await new Promise(resolve => setTimeout(resolve,100));
        }
        throw new Error('settings surface timeout');
      })()`
    : operation === 'clear-all'
    ? `(async () => {
        window.chrome.webview.postMessage({action:'clearAll'});
        for (let i=0;i<80;i++) {
          if (document.querySelectorAll('.record').length === 0) return { cleared:true };
          await new Promise(resolve=>setTimeout(resolve,100));
        }
        throw new Error('isolated history clear timeout');
      })()`
    : operation === 'seed-visual-fixture'
    ? `(() => {
        const list=document.querySelector('#recordList');
        const count=document.querySelector('#countText');
        if(!list||!count)throw new Error('record surface missing');
        const fixtures=[
          ['文本','Huahai Clipboard visual acceptance','just now · Test fixture','&#xE8D2;'],
          ['链接','https://github.com/xy2446522127-code/huahaibanzi','2 min ago · Test fixture','&#xE71B;'],
          ['文本','Safe update: download, verify, rollback, restart','5 min ago · Test fixture','&#xE8D2;'],
          ['文件','HuahaiClipboard-Setup.exe','8 min ago · Test fixture','&#xE8A5;'],
          ['文本','Favorites and pinned records survive cleanup','12 min ago · Test fixture','&#xE8D2;'],
          ['图片','fox-icon-preview.png','15 min ago · Test fixture','&#xE8B9;']
        ];
        list.innerHTML=fixtures.map((item,index)=>'<div class="record" data-id="fixture-'+index+'"><div class="kind" title="'+item[0]+'"><span class="kind-glyph" aria-hidden="true">'+item[3]+'</span></div><div class="record-text"><strong>'+item[1]+'</strong><small>'+(index===0?'置顶 · ':'')+item[2]+'</small></div><div class="row-actions"><button class="row-action pin '+(index===0?'on':'')+'" title="置顶"><span class="pin-glyph" aria-hidden="true">&#xE718;</span></button><button class="row-action fav '+(index===4?'on':'')+'" title="收藏">★</button><button class="row-action del" title="删除">×</button></div></div>').join('');
        count.textContent='最近 7 天 · '+fixtures.length+' 条';
        return {seeded:true,count:fixtures.length};
      })()`
    : operation === 'hit-test'
    ? `(() => {
        const [x,y]=${quoted}.split(',').map(Number);
        const element=document.elementFromPoint(x,y);
        if (!element) throw new Error('no element at requested point');
        return {
          x,y,
          tag:element.tagName,
          id:element.id || '',
          classes:element.className || '',
          interactive:window.HuahaiPanelDrag.isInteractiveTarget(element),
          panelRect:(() => { const r=document.querySelector('#glassPanel').getBoundingClientRect(); return {left:r.left,top:r.top,right:r.right,bottom:r.bottom}; })()
        };
      })()`
    : operation === 'arm-pointer-log'
    ? `(() => {
        window.__huahaiPointerAudit=[];
        const record=event=>window.__huahaiPointerAudit.push({type:event.type,clientX:event.clientX,clientY:event.clientY,screenX:event.screenX,screenY:event.screenY,pointerId:event.pointerId,target:event.target?.className||event.target?.id||event.target?.tagName||''});
        for (const type of ['pointerdown','pointermove','pointerup','pointercancel']) document.addEventListener(type,record,{capture:true});
        return {armed:true};
      })()`
    : operation === 'read-pointer-log'
    ? `(() => ({events:(window.__huahaiPointerAudit||[]).slice(-20)}))()`
    : operation === 'resize-grab-point'
    ? `(() => {
        const panel=document.querySelector('#glassPanel').getBoundingClientRect();
        const handle=document.querySelector('#resizeHandle');
        const handleRect=handle.getBoundingClientRect();
        const pseudo=getComputedStyle(handle,'::after');
        const x=handleRect.right-parseFloat(pseudo.right)-1;
        const y=handleRect.bottom-parseFloat(pseudo.bottom)-1;
        return {xRatio:(x-panel.left)/panel.width,yRatio:(y-panel.top)/panel.height,pseudoRight:parseFloat(pseudo.right),pseudoBottom:parseFloat(pseudo.bottom)};
      })()`
    : operation === 'set-scale'
    ? `(async () => {
        const target=Number(${quoted});
        if (!Number.isFinite(target)) throw new Error('invalid scale');
        const input=document.querySelector('#scaleRange');
        if (!input) throw new Error('scale control missing');
        input.value=String(Math.round(target*100));
        input.dispatchEvent(new Event('input',{bubbles:true}));
        for (let i=0;i<50;i++) {
          const actual=Number(getComputedStyle(document.documentElement).getPropertyValue('--panel-scale'));
          if (Math.abs(actual-target)<0.001) return { scaled:true, target, actual, label:document.querySelector('#scaleValue')?.textContent || '' };
          await new Promise(resolve=>setTimeout(resolve,100));
        }
        throw new Error('scale state timeout');
      })()`
    : operation === 'check-update'
    ? `(async () => {
        const button=document.querySelector('#checkUpdateButton');
        const status=document.querySelector('#updateStatus');
        if (!button || !status) throw new Error('update controls missing');
        button.click();
        for (let i=0;i<150;i++) {
          if (!button.disabled && !status.classList.contains('checking')) return { completed:true, statusClass:status.className, message:status.textContent || '' };
          await new Promise(resolve=>setTimeout(resolve,100));
        }
        throw new Error('update check timeout');
      })()`
    : operation === 'delete-prefix'
    ? `(async () => {
        const prefix=${quoted};
        let deleted=0;
        for (let pass=0;pass<100;pass++) {
          const row=[...document.querySelectorAll('.record')].find(candidate => candidate.querySelector('.record-text strong')?.textContent.startsWith(prefix));
          if (!row) return { deleted, prefix };
          row.querySelector('.del').click();
          deleted++;
          await new Promise(resolve => setTimeout(resolve,100));
        }
        throw new Error('smoke cleanup exceeded its bounded record count');
      })()`
    : operation === 'inspect-and-toggle'
    ? `(async () => {
        const expected=${quoted};
        const wait = async (predicate, label) => { for (let i=0;i<80;i++){const result=predicate();if(result)return result;await new Promise(r=>setTimeout(r,100));}throw new Error(label + ' timeout'); };
        const first = await wait(() => expected
          ? [...document.querySelectorAll('.record')].find(row => row.querySelector('.record-text strong')?.textContent === expected)
          : document.querySelector('.record'), 'initial record');
        const id = first.dataset.id;
        const row = () => document.querySelector('.record[data-id="' + id + '"]');
        const text = first.querySelector('.record-text strong')?.textContent || '';
        const countText = document.querySelector('#countText')?.textContent || '';
        const count = Number(countText.match(/(\\d+)\\s*条/)?.[1] || -1);
        const visibleRows = document.querySelectorAll('.record').length;
        if (!text || count < visibleRows || visibleRows < 1) throw new Error('live count does not match visible history');
        const toggleAndRestore = async selector => {
          const before = row().querySelector(selector).classList.contains('on');
          row().querySelector(selector).click();
          await wait(() => row() && row().querySelector(selector).classList.contains('on') !== before, selector + ' toggle');
          row().querySelector(selector).click();
          await wait(() => row() && row().querySelector(selector).classList.contains('on') === before, selector + ' restore');
          return before;
        };
        const pinBefore = await toggleAndRestore('.pin');
        const favoriteBefore = await toggleAndRestore('.fav');
        return { id, text, countText, count, visibleRows, pinRestored: row().querySelector('.pin').classList.contains('on') === pinBefore, favoriteRestored: row().querySelector('.fav').classList.contains('on') === favoriteBefore };
      })()`
    : operation === 'wait-and-delete-text'
      ? `(async () => {
          const expected=${quoted};
          const wait = async predicate => { for (let i=0;i<100;i++){const result=predicate();if(result)return result;await new Promise(r=>setTimeout(r,100));}throw new Error('delete probe timeout'); };
          const find = () => [...document.querySelectorAll('.record')].find(row => row.querySelector('.record-text strong')?.textContent === expected);
          const row = await wait(find);
          row.querySelector('.del').click();
          await wait(() => !find());
          return { deleted: true, text: expected };
        })()`
      : operation === 'copy-id'
        ? `(async () => {
            const id=${quoted};
            const wait = async predicate => { for (let i=0;i<80;i++){const result=predicate();if(result)return result;await new Promise(r=>setTimeout(r,100));}throw new Error('copy probe timeout'); };
            const row = await wait(() => document.querySelector('.record[data-id="' + id + '"]'));
            const autoHide = document.querySelector('#autoHide');
            if (autoHide) autoHide.checked = false;
            const text = row.querySelector('.record-text strong')?.textContent || '';
            row.click();
            return { clicked: true, id, text };
          })()`
        : `(() => { throw new Error('Unknown operation'); })()`;

  const evaluation = await command('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
  socket.close();
  if (evaluation.exceptionDetails) throw new Error(evaluation.exceptionDetails.text || 'WebView evaluation failed.');
  if (evaluation.result.value == null) throw new Error(`WebView evaluation returned no value: ${JSON.stringify(evaluation)}`);
  console.log(JSON.stringify(evaluation.result.value));
}

run().catch(error => {
  console.error(error.stack || error.message);
  process.exitCode = 1;
});
