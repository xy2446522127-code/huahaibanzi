# History Scale Performance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the existing Flower Sea Clipboard experience responsive with 1,000 history records while preserving complete search, filtering, sorting, thumbnails, and record actions.

**Architecture:** Add a pure virtual-window calculation module and use it from the approved `product-shell.html` so only the visible record slice exists in the DOM. Keep the current native full-state bridge for this stage, coalesce scroll rendering to one animation-frame callback, and replace per-row listeners with one delegated list listener.

**Tech Stack:** JavaScript/CommonJS-compatible browser module, Node.js built-in test runner, HTML/CSS/JS WebView2 shell, PowerShell desktop smoke adapter, .NET 8/WinUI 3 host.

## Global Constraints

- Preserve the approved `product-shell.html` visual design, DOM control IDs, themes, fonts, glass material, row icons, thumbnail behavior, and interaction contract.
- Preserve the existing history file format and native full-state WebView message for the first stage.
- Search, filters, pinned ordering, favorites, deletion, and copy must operate over all records, not only rendered records.
- With 1,000 anonymous records, render no more than 80 `.record` elements and keep state-apply-and-paint P95 at or below 50ms.
- A 20-sample top-to-bottom scroll path must have no blank window, duplicate visible ID, rollback, or missing terminal record.
- Clipboard tests use anonymous fixtures and an isolated data root; never read, print, delete, or commit user clipboard history.
- Restore `F:\HuahaiClipboard\HuahaiClipboard.App.exe --background` after every candidate-app run.

---

### Task 1: Pure virtual record window

**Files:**
- Create: `src/HuahaiClipboard.App/Assets/Web/virtual-record-list.js`
- Create: `tests/VirtualRecordListTests.cjs`
- Modify: `src/HuahaiClipboard.App/HuahaiClipboard.App.csproj`

**Interfaces:**
- Produces: `HuahaiVirtualRecordList.calculateWindow({ itemCount, scrollTop, viewportHeight, rowExtent, overscan })` returning `{ start, end, topSpacer, bottomSpacer }`, where `end` is exclusive.
- Produces: `HuahaiVirtualRecordList.createFrameScheduler({ scheduleFrame, cancelFrame, render })` returning `{ request(value), flush(), dispose() }`.

- [ ] **Step 1: Write the failing calculation tests**

```js
const virtualList = require('../src/HuahaiClipboard.App/Assets/Web/virtual-record-list.js');

test('1,000 records render only a bounded visible window', () => {
  const result = virtualList.calculateWindow({
    itemCount: 1000, scrollTop: 35000, viewportHeight: 520,
    rowExtent: 76, overscan: 4,
  });
  assert.ok(result.end - result.start <= 16);
  assert.equal(result.topSpacer, result.start * 76);
  assert.equal(result.bottomSpacer, (1000 - result.end) * 76);
});

test('the final scroll position includes the final record without overflow', () => {
  const result = virtualList.calculateWindow({
    itemCount: 1000, scrollTop: 76000, viewportHeight: 520,
    rowExtent: 76, overscan: 4,
  });
  assert.equal(result.end, 1000);
  assert.ok(result.start >= 980);
});
```

- [ ] **Step 2: Run the tests and verify RED**

Run: `node --test tests/VirtualRecordListTests.cjs`

Expected: FAIL because `virtual-record-list.js` does not exist.

- [ ] **Step 3: Implement the minimal pure module**

```js
function calculateWindow({ itemCount, scrollTop, viewportHeight, rowExtent, overscan = 4 }) {
  const count = Math.max(0, Math.trunc(itemCount));
  if (count === 0) return { start: 0, end: 0, topSpacer: 0, bottomSpacer: 0 };
  const extent = Math.max(1, Number(rowExtent));
  const first = Math.floor(Math.max(0, Number(scrollTop)) / extent);
  const visible = Math.ceil(Math.max(0, Number(viewportHeight)) / extent);
  const start = Math.max(0, first - overscan);
  const end = Math.min(count, first + visible + overscan);
  return {
    start, end,
    topSpacer: start * extent,
    bottomSpacer: (count - end) * extent,
  };
}
```

Implement the scheduler so repeated `request` calls before the next frame retain only the latest value and invoke `render` once.

- [ ] **Step 4: Package the module and verify GREEN**

Add the new file to the explicit Web asset `Content` list in the app project, then run:

`node --test tests/VirtualRecordListTests.cjs`

Expected: all virtual-window and frame-coalescing tests pass.

- [ ] **Step 5: Create the functional checkpoint**

Checkpoint only the module, its tests, and the project content entry with message `perf: add bounded virtual record window`.

---

### Task 2: Integrate virtualization and delegated actions into the approved shell

**Files:**
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Create: `tests/VirtualRecordListShellTests.cjs`
- Modify: `tests/CompletePrototypeExperienceTests.cjs`

**Interfaces:**
- Consumes: `HuahaiVirtualRecordList.calculateWindow` and `createFrameScheduler` from Task 1.
- Produces: `renderItems({ preserveScroll = true } = {})`, which filters and sorts the full `items` array but renders only the computed slice.
- Produces: one delegated `click` listener on `#recordList` routing `.pin`, `.fav`, `.del`, and row-copy actions by `data-id`.

- [ ] **Step 1: Write failing shell contract tests**

```js
test('the formal shell loads and uses the virtual record module', () => {
  assert.match(html, /<script src="virtual-record-list\.js"><\/script>/);
  assert.match(html, /HuahaiVirtualRecordList\.calculateWindow/);
  assert.match(html, /HuahaiVirtualRecordList\.createFrameScheduler/);
});

test('record actions are delegated once from the list container', () => {
  assert.match(html, /recordList\.addEventListener\('click'/);
  assert.doesNotMatch(html, /hhQA\('\.record'\)\.forEach\(row=>/);
});
```

- [ ] **Step 2: Run the tests and verify RED**

Run: `node --test tests/VirtualRecordListShellTests.cjs tests/CompletePrototypeExperienceTests.cjs`

Expected: the new virtualization and delegation assertions fail against the current full-list renderer.

- [ ] **Step 3: Add spacer-safe virtual markup without visual drift**

Load `virtual-record-list.js` before the module script. Render this structure inside the existing scroll container:

```html
<div class="virtual-spacer top" aria-hidden="true"></div>
<!-- existing .record markup for the visible slice only -->
<div class="virtual-spacer bottom" aria-hidden="true"></div>
```

Use the existing card markup unchanged. Derive `rowExtent` from the first rendered record's actual outer height, including vertical margins, and fall back to `76` only before the first measurement. Recalculate on panel-scale changes and `ResizeObserver` notifications.

- [ ] **Step 4: Add one frame-coalesced scroll pipeline**

```js
const virtualRenderScheduler = window.HuahaiVirtualRecordList.createFrameScheduler({
  scheduleFrame: callback => requestAnimationFrame(callback),
  cancelFrame: id => cancelAnimationFrame(id),
  render: () => renderVisibleRecords(),
});
recordList.addEventListener('scroll', () => virtualRenderScheduler.request(recordList.scrollTop), { passive: true });
```

On state, filter, search, pin, favorite, and delete changes, recalculate the filtered full list and schedule one visible render. Preserve `scrollTop` unless the new range makes it invalid; then clamp to the nearest legal position.

- [ ] **Step 5: Replace per-row listeners with delegated routing**

```js
recordList.addEventListener('click', event => {
  const row = event.target.closest('.record');
  if (!row || !recordList.contains(row)) return;
  const item = items.find(value => String(value.id) === row.dataset.id);
  if (!item) return;
  if (event.target.closest('.pin')) return invokePin(item);
  if (event.target.closest('.fav')) return invokeFavorite(item);
  if (event.target.closest('.del')) return invokeDelete(item);
  invokeCopy(item, row);
});
```

Keep native/prototype branches, toast copy, immediate native copy request, thumbnail observation, and interaction-contract tagging behavior identical.

- [ ] **Step 6: Verify shell behavior and protected UI contracts**

Run:

- `node --test tests/VirtualRecordListShellTests.cjs tests/CompletePrototypeExperienceTests.cjs`
- `node --test tests/WebShellModuleTests.cjs tests/InteractionContractTests.cjs`
- `dotnet build src/HuahaiClipboard.App/HuahaiClipboard.App.csproj -c Release -p:Platform=x64 --no-restore`

Expected: all tests pass and the Web asset is copied to the build output.

- [ ] **Step 7: Create the functional checkpoint**

Checkpoint only `product-shell.html`, `VirtualRecordListShellTests.cjs`, and `CompletePrototypeExperienceTests.cjs` with message `perf: virtualize clipboard history rendering`.

---

### Task 3: Exact WebView scale and continuous-scroll performance evidence

**Files:**
- Create: `tests/HuahaiClipboard.App.Smoke/HistoryScalePerformanceProbe.cjs`
- Create: `tests/HuahaiClipboard.App.Smoke/HistoryScalePerformanceSmoke.ps1`

**Interfaces:**
- Consumes: a candidate `HuahaiClipboard.App.exe`, an isolated `HUAHAI_CLIPBOARD_LOCALAPPDATA`, and a fixed leased debug port.
- Produces: compact JSON containing per-count P50/P95/max, DOM row count, 20 scroll samples, duplicate/blank/rollback counts, and final-record visibility.

- [ ] **Step 1: Turn the current 1,000-row regression into a red-capable tracked smoke test**

The probe applies anonymous 100/500/1000-record states through `window.HuahaiApplyNativeState`, forces layout and two animation frames, and fails when:

```js
if (count === 1000 && document.querySelectorAll('.record').length > 80) {
  throw new Error('virtual DOM row budget exceeded');
}
if (p95 > 50) throw new Error(`render P95 exceeded 50 ms: ${p95}`);
if (blankSamples || duplicateSamples || rollbackSamples || !finalRecordVisible) {
  throw new Error('continuous scroll contract failed');
}
```

- [ ] **Step 2: Run against the pre-virtualization candidate and verify RED**

Run: `powershell -ExecutionPolicy Bypass -File tests/HuahaiClipboard.App.Smoke/HistoryScalePerformanceSmoke.ps1 -ExePath dist/diagnostic-latency-fix3-20260811/HuahaiClipboard.App.exe`

Expected: FAIL because 1,000 `.record` elements exist and P95 is approximately 101ms on the established machine baseline.

- [ ] **Step 3: Run against the post-fix candidate and verify GREEN three times**

Publish with `dotnet publish src/HuahaiClipboard.App/HuahaiClipboard.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o dist/history-scale-performance-candidate`, then execute the exact smoke command three times against `dist/history-scale-performance-candidate/HuahaiClipboard.App.exe`. Each run must satisfy the thresholds and clean its port, process, profile, and anonymous data in `finally`.

- [ ] **Step 4: Verify record actions after scrolling**

Use the same CDP session to scroll a record outside the initial window into view, then invoke and verify pin, favorite, delete, thumbnail request, and copy messages by independent bridge-message oracles. No action may rely on a stale detached row.

- [ ] **Step 5: Create the evidence checkpoint**

Checkpoint the reusable performance probe and smoke wrapper with message `test: cover large clipboard history performance`.

---

### Task 4: Integrate with the pending copy and activation fixes

**Files:**
- Verify existing pending changes in `WindowsClipboardPlatform.cs`, `CursorPanelWindow.xaml.cs`, `TransientWindowVisibilityController.cs`, `Program.cs`, `ExternalActivationSignal.cs`, `launcher/`, and their tests.
- Create: `tests/LauncherActivationPerformanceSmoke.ps1`
- Do not change installer version or create a release in this task.

**Interfaces:**
- Consumes: the virtualized shell and existing asynchronous clipboard/fast activation candidates.
- Produces: one buildable candidate in which summon, copy, hidden live history, large-list rendering, and runtime cleanup all pass together.

- [ ] **Step 1: Run focused copy, visibility, and launcher tests**

Run:

- `powershell -ExecutionPolicy Bypass -File tests/LauncherActivationTests.ps1`
- `dotnet test tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~ExternalActivationSignalTests|FullyQualifiedName~WindowsClipboardPlatformTests" --no-restore`
- `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj -c Release --filter "FullyQualifiedName~TransientWindowVisibilityControllerTests" --no-restore`

- [ ] **Step 2: Run affected Web and desktop integration suites**

Run `Get-ChildItem tests -File -Filter *.cjs | ForEach-Object { node --test $_.FullName; if ($LASTEXITCODE -ne 0) { throw "Node test failed: $($_.Name)" } }` and `dotnet test HuahaiClipboard.sln -c Release -p:Platform=x64 --no-restore` once for the integration milestone.

- [ ] **Step 3: Build and run the final isolated candidate**

Verify 36 launcher summons have zero samples over 250ms, copy hides before the simulated 250ms clipboard write completes, and the 100/500/1000 scale smoke remains green. Always restore the installed F-drive background instance afterward.

- [ ] **Step 4: Review the diff and delivery evidence**

Run requirement fidelity, engineering integrity, and evidence/provenance review. Confirm no UI visual selectors, history format, installer target, update endpoint, user data, browser profile, or `.codex/artifacts` file is staged.

- [ ] **Step 5: Create the integration checkpoint**

Checkpoint only verified product/test paths. Do not push, publish, install, or create GitHub Release without a separate authorized release step.
