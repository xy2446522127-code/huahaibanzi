const assert = require('node:assert/strict');
const vm = require('node:vm');
const { buildExpression } = require('./WebViewHideButtonExpression.cjs');

async function testHideAcknowledgesBeforeTheWindowIsHidden() {
  let clicks = 0;
  const deferred = [];
  const button = {
    click: () => { clicks += 1; },
    getAttribute: name => name === 'title' ? '隐藏到后台' : null,
  };
  const toggle = {
    click: () => {},
    classList: { contains: () => false },
  };
  const context = {
    document: {
      querySelector: selector => selector === '#minimizeButton' ? button : toggle,
    },
    setTimeout: (callback, delay) => {
      if (delay === 0) deferred.push(callback);
      else callback();
    },
  };

  const result = await vm.runInNewContext(buildExpression('hide-with-background-disabled'), context);

  assert.equal(result.clicked, true);
  assert.equal(clicks, 0, 'the CDP result must be available before the hide click suspends WebView');
  assert.equal(deferred.length, 1);
  deferred[0]();
  assert.equal(clicks, 1);
}

testHideAcknowledgesBeforeTheWindowIsHidden()
  .then(() => process.stdout.write(JSON.stringify({ status: 'passed', tests: 1 })))
  .catch(error => {
    console.error(error.stack || error.message);
    process.exitCode = 1;
  });
