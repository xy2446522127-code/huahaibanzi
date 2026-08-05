const port = Number(process.argv[2]);
const operation = process.argv[3] || 'hide-with-background-disabled';
if (!Number.isInteger(port) || port <= 0) throw new Error('A valid WebView2 debugging port is required.');

// 连接 WebView2 的 CDP 页面并真实点击“隐藏到后台”按钮。
async function run() {
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

  // 发送单条 CDP 命令并等待对应响应，避免依赖页面日志文本。
  const command = (method, params = {}) => new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });

  const expression = operation === 'restore-background'
    ? `(() => {
      const toggle = document.querySelector('#backgroundToggle');
      if (!toggle) return { restored: false, reason: 'missing-toggle' };
      if (!toggle.classList.contains('on')) toggle.click();
      return { restored: toggle.classList.contains('on') };
    })()`
    : `(async () => {
      const button = document.querySelector('#minimizeButton');
      const toggle = document.querySelector('#backgroundToggle');
      if (!button || !toggle) return { clicked: false, reason: 'missing-control' };
      if (toggle.classList.contains('on')) toggle.click();
      await new Promise(resolve => setTimeout(resolve, 350));
      const title = button.getAttribute('title');
      button.click();
      return { clicked: true, title, backgroundEnabled: toggle.classList.contains('on') };
    })()`;

  const evaluation = await command('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true
  });

  socket.close();
  const value = evaluation.result.value;
  if (operation === 'restore-background') {
    if (!value?.restored) throw new Error(`Unexpected background restore result: ${JSON.stringify(value)}`);
  } else if (!value?.clicked || value.title !== '隐藏到后台' || value.backgroundEnabled !== false) {
    throw new Error(`Unexpected minimize button result: ${JSON.stringify(value)}`);
  }

  process.stdout.write(JSON.stringify(value));
}

run().catch(error => {
  console.error(error.stack || error.message);
  process.exitCode = 1;
});
