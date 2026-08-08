const fs = require('node:fs');
const test = require('node:test');
const assert = require('node:assert/strict');

const windowHost = fs.readFileSync(
  'src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs',
  'utf8');

test('desktop host owns one proactive coordinator and disposes it', () => {
  assert.match(windowHost, /ProactiveUpdateCoordinator\? updateCoordinator/);
  assert.match(windowHost, /TryStartUpdateCoordinator\(\)/);
  assert.match(windowHost, /updateCoordinator\?\.DisposeAsync\(\)/);
});

test('startup check waits until both the Web shell and tray are ready', () => {
  assert.match(windowHost, /updateStartupGate\.TryBegin\(shellReady, trayService is not null\)/);
  assert.equal((windowHost.match(/TryStartUpdateCoordinator\(\);/g) || []).length, 3);
});

test('all update results share tray badge and notification state', () => {
  assert.match(windowHost, /HandleUpdateResultAsync\(/);
  assert.match(windowHost, /trayService\?\.SetUpdateAvailable\(/);
  assert.match(windowHost, /trayService\?\.NotifyUpdateAvailable\(/);
  assert.match(windowHost, /notifyUser:/);
});

test('snooze persists for the current release and lasts 24 hours', () => {
  assert.match(windowHost, /case "snoozeUpdate":/);
  assert.match(windowHost, /UpdateReminderPolicy\.SnoozeDuration/);
  assert.match(windowHost, /SnoozedUpdateVersion/);
  assert.match(windowHost, /UpdateSnoozeUntil/);
});
