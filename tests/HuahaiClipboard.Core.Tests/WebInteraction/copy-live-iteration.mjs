import { createRequire } from 'node:module';
import fs from 'node:fs/promises';
import path from 'node:path';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright');
const [url, screenshotPath] = process.argv.slice(2);
if (!url || !screenshotPath) {
  throw new Error('Usage: node copy-live-iteration.mjs <url> <screenshot-path>');
}

const browser = await chromium.launch({
  headless: true,
  executablePath: process.env.HUAHAI_EDGE_PATH,
  args: ['--disable-gpu']
});
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
const errors = [];
page.on('pageerror', error => errors.push(`pageerror:${error.message}`));
page.on('console', message => {
  if (message.type() === 'error') {
    const location = message.location();
    if (location.url?.endsWith('/favicon.ico')) return;
    errors.push(`console:${message.text()}:${location.url || 'unknown'}`);
  }
});

try {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.evaluate(() => localStorage.removeItem('huahai.prototype.outsideAutoHide'));
  await page.reload({ waitUntil: 'networkidle' });

  const records = page.locator('#recordList .record');
  const initialCount = await records.count();
  const initialFirst = await records.first().locator('.record-text strong').textContent();
  if (initialCount < 2) throw new Error(`Expected realistic history, got ${initialCount} records.`);

  await page.locator('#settingsButton').click();
  await page.locator('.nav-button[data-page="system"]').click();
  const outsideToggle = page.locator('#outsideAutoHideToggle');
  if (!(await outsideToggle.evaluate(element => element.classList.contains('on')))) {
    throw new Error('Outside-hide setting must default to enabled.');
  }

  await outsideToggle.click();
  if (await outsideToggle.evaluate(element => element.classList.contains('on'))) {
    throw new Error('Outside-hide setting did not turn off.');
  }
  await page.reload({ waitUntil: 'networkidle' });
  if (await page.locator('#outsideAutoHideToggle').evaluate(element => element.classList.contains('on'))) {
    throw new Error('Disabled outside-hide setting did not persist across reload.');
  }

  await page.locator('#settingsHome').click();
  await records.first().click();
  if (!(await page.locator('#glassPanel').evaluate(element => element.classList.contains('hidden')))) {
    throw new Error('Copy did not immediately hide the panel.');
  }
  await page.locator('#summonButton').click();
  const afterCopyCount = await records.count();
  const afterCopyFirst = await records.first().locator('.record-text strong').textContent();
  if (afterCopyCount !== initialCount || afterCopyFirst !== initialFirst) {
    throw new Error('Panel-originated copy changed record count or first-record ordering.');
  }

  await page.mouse.click(200, 300);
  if (await page.locator('#glassPanel').evaluate(element => element.classList.contains('hidden'))) {
    throw new Error('Panel hid even though outside-hide was disabled.');
  }

  await page.locator('#settingsButton').click();
  await page.locator('.nav-button[data-page="system"]').click();
  await page.locator('#outsideAutoHideToggle').click();
  await page.locator('#settingsHome').click();
  await page.mouse.click(200, 300);
  if (!(await page.locator('#glassPanel').evaluate(element => element.classList.contains('hidden')))) {
    throw new Error('Panel stayed visible after an outside click with the setting enabled.');
  }

  await page.locator('#summonButton').click();
  await fs.mkdir(path.dirname(screenshotPath), { recursive: true });
  await page.screenshot({ path: screenshotPath, fullPage: true });
  if (errors.length) throw new Error(errors.join('\n'));
  console.log(JSON.stringify({
    passed: true,
    initialCount,
    afterCopyCount,
    firstRecordUnchanged: afterCopyFirst === initialFirst,
    outsideHidePersisted: true,
    consoleErrors: 0
  }));
} finally {
  await browser.close();
}
