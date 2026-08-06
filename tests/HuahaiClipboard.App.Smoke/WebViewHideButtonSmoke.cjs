const port = Number(process.argv[2]);
const operation = process.argv[3] || 'hide-with-background-disabled';
const { buildExpression } = require('./WebViewHideButtonExpression.cjs');
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
    clearTimeout(request.timer);
    if (message.error) request.reject(new Error(message.error.message));
    else request.resolve(message.result);
  });

  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });

  // 发送单条 CDP 命令并等待对应响应，避免依赖页面日志文本。
  socket.addEventListener('close', () => {
    for (const [id, request] of pending) {
      clearTimeout(request.timer);
      request.reject(new Error(`CDP socket closed before response ${id}.`));
    }
    pending.clear();
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

  const expression = buildExpression(operation);

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
