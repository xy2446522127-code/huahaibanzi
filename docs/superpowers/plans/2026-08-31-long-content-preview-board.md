# Long Content Preview Board Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an editable independent preview window for clipboard records, opened by history-card right click or a hovered-record shortcut, while preserving existing clipboard payloads and approved Huahai visuals.

**Architecture:** `product-shell.html` stays the only WebView document and renders a `surface=preview` route in a second WebView2 host. A native `PreviewWindowCoordinator` reuses one `ContentPreviewWindow`; Core owns preview edit persistence, validation, shortcut lease policy, and preview placement data.

**Tech Stack:** .NET 8, WinUI 3, WebView2, Win32 Shell APIs, MSTest, Node CDP smoke tests, PowerShell desktop smoke tests.

## Global Constraints

- Target `net8.0-windows10.0.19041.0` on Windows x64 and add no third-party dependency.
- Keep `ClipboardRecord.PrimaryText` as the original paste payload. Image/file changes persist only in nullable `DisplayName`.
- Preserve ID, copy time, source, pin, favorite, availability, preview asset, and canonical paths on every save.
- Old history/settings load with null `DisplayName` and null `PreviewShortcut`.
- Keep `src/HuahaiClipboard.App/Assets/Web/product-shell.html` as the only HTML shell.
- Reuse the exact installed pin paths, ruby fill, and favorite-star markup; do not replace them with generic icons.
- Right click leaves the main panel visible and positions preview left of it with an 18 logical-pixel gap. Main Hide and left-click-copy auto-hide remain unchanged.
- Preview default is `650 x 500`, minimum `420 x 360`, with independent geometry/topmost persistence and a 250 ms clean/unfocused auto-hide delay.
- Images use protected in-memory URLs; file Shell thumbnails/type icons create no plaintext cache.
- PaperTodo, todos, publishing, installation, deployment, and external code are not in scope.

## File Structure

| Path | Responsibility |
| --- | --- |
| `src/HuahaiClipboard.Core/Models/ClipboardRecord.cs` | Add nullable display name. |
| `src/HuahaiClipboard.Core/Models/PreviewEdit.cs` | Define typed preview edit input/result. |
| `src/HuahaiClipboard.Core/Contracts/IClipboardHistorySource.cs` | Add atomic preview edit API. |
| `src/HuahaiClipboard.Core/Services/ClipboardRecordEditor.cs` | Validate and apply preview edits. |
| `src/HuahaiClipboard.Core/Services/PreviewShortcutLeasePolicy.cs` | Decide shortcut eligibility. |
| `src/HuahaiClipboard.Core/Services/PreviewWindowPlacementStore.cs` | Persist preview bounds/topmost. |
| `src/HuahaiClipboard.App/Infrastructure/Preview/WindowsShellPreviewSource.cs` | Resolve safe file preview data. |
| `src/HuahaiClipboard.App/Presentation/Windows/ContentPreviewWindow.xaml(.cs)` | Native preview window and WebView host. |
| `src/HuahaiClipboard.App/Presentation/Windows/PreviewWindowCoordinator.cs` | Reuse, place, and refresh one preview window. |
| `src/HuahaiClipboard.App/Assets/Web/product-shell.html` | Approved main/preview routes in one shell. |

---

### Task 1: Persist and Validate Preview Edits

**Files:**
- Modify: `src/HuahaiClipboard.Core/Models/ClipboardRecord.cs`
- Create: `src/HuahaiClipboard.Core/Models/PreviewEdit.cs`
- Modify: `src/HuahaiClipboard.Core/Contracts/IClipboardHistorySource.cs`
- Modify: `src/HuahaiClipboard.Core/Services/JsonClipboardHistorySource.cs`
- Create: `src/HuahaiClipboard.Core/Services/ClipboardRecordEditor.cs`
- Modify: `src/HuahaiClipboard.Core/Services/ClipboardRecordDisplay.cs`
- Modify: `src/HuahaiClipboard.Core/Presentation/PanelViewModel.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/ClipboardRecordDisplayTests.cs`
- Create: `tests/HuahaiClipboard.Core.Tests/ClipboardRecordEditorTests.cs`

**Interfaces:**
- Produces `PreviewEdit`, `PreviewEditResult`, `IClipboardHistorySource.ApplyPreviewEditAsync`, and `PanelViewModel.SavePreviewAsync`.

- [ ] **Step 1: Write failing Core tests**

```csharp
[TestMethod]
public async Task FileRename_ChangesDisplayNameWithoutChangingPayload()
{
    var updated = await source.ApplyPreviewEditAsync(file.Id,
        new PreviewEdit(ClipboardItemKind.File, "发布计划"), CancellationToken.None);
    Assert.AreEqual(file.PrimaryText, updated!.PrimaryText);
    Assert.AreEqual("发布计划", updated.DisplayName);
}
```

- [ ] **Step 2: Run failing tests**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~ClipboardRecordEditorTests"`

Expected: compile failure before the new API exists.

- [ ] **Step 3: Implement minimal model and persistence**

```csharp
public sealed record PreviewEdit(ClipboardItemKind ExpectedKind, string Value);
public sealed record PreviewEditResult(ClipboardRecord? Record, string? ErrorMessage, bool ConvertedLinkToText);
Task<ClipboardRecord?> ApplyPreviewEditAsync(Guid id, PreviewEdit edit, CancellationToken cancellationToken);
```

Use the existing history mutation gate. Text/link writes replace `PrimaryText`; file/image writes replace `DisplayName`; missing records return null. Reject blank values; convert non-HTTP(S) link text to `ClipboardItemKind.Text`; refresh `AllRecords` and `VisibleRecords` without changing ordering.

- [ ] **Step 4: Cover legacy JSON and deleted-record recovery**

```csharp
Assert.IsNull(await source.ApplyPreviewEditAsync(Guid.NewGuid(),
    new PreviewEdit(ClipboardItemKind.Text, "draft"), CancellationToken.None));
```

- [ ] **Step 5: Run Core suite and commit**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj`

Commit message: `feat: persist preview record edits`

### Task 2: Add Preview Shortcut and Placement Policies

**Files:**
- Modify: `src/HuahaiClipboard.Core/Settings/InputSettings.cs`
- Create: `src/HuahaiClipboard.Core/Services/PreviewShortcutLeasePolicy.cs`
- Create: `src/HuahaiClipboard.Core/Services/PreviewWindowPlacementStore.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/JsonSettingsStoreTests.cs`
- Create: `tests/HuahaiClipboard.Core.Tests/PreviewShortcutLeasePolicyTests.cs`
- Create: `tests/HuahaiClipboard.Core.Tests/PreviewWindowPlacementStoreTests.cs`

**Interfaces:**
- Produces `InputSettings.PreviewShortcut`, `PreviewShortcutLeasePolicy.ShouldLease`, and `PreviewWindowPlacementStore`.

- [ ] **Step 1: Write failing policy tests**

```csharp
Assert.IsTrue(PreviewShortcutLeasePolicy.ShouldLease(true, true, false, "Ctrl+Alt+P"));
Assert.IsFalse(PreviewShortcutLeasePolicy.ShouldLease(true, false, false, "Ctrl+Alt+P"));
```

- [ ] **Step 2: Run failing policy tests**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~PreviewShortcutLeasePolicyTests|FullyQualifiedName~PreviewWindowPlacementStoreTests"`

Expected: compile failure before policy and store exist.

- [ ] **Step 3: Implement settings, policy, and geometry**

```csharp
public sealed record InputSettings(bool RightDoubleClickEnabled, bool HotkeyEnabled,
    string[] ExcludedApplications, string? CustomShortcut = null, string? PreviewShortcut = null);
public sealed record PreviewWindowPlacement(string DisplayId, int X, int Y,
    int Width, int Height, bool Topmost);
```

Lease only for a visible main panel, hovered record, closed settings, and valid keyboard gesture. Reject a normalized match to the summon shortcut. Use a dedicated JSON placement file; clamp to active work area with 16-pixel margin, default 650 x 500, minimum 420 x 360.

- [ ] **Step 4: Verify legacy settings and geometry clamping**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~JsonSettingsStoreTests|FullyQualifiedName~PreviewShortcutLeasePolicyTests|FullyQualifiedName~PreviewWindowPlacementStoreTests"`

- [ ] **Step 5: Commit policy checkpoint**

Commit message: `feat: add preview shortcut and placement policies`

### Task 3: Create Safe Preview Asset Sources

**Files:**
- Create: `src/HuahaiClipboard.App/Infrastructure/Preview/WindowsShellPreviewSource.cs`
- Modify: `src/HuahaiClipboard.App/CompositionRoot.cs`
- Create: `tests/HuahaiClipboard.App.IntegrationTests/WindowsShellPreviewSourceTests.cs`
- Modify: `tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj`

**Interfaces:**
- Produces `IPreviewThumbnailSource.CreateAsync(ClipboardRecord, CancellationToken)` and `PreviewThumbnailResult`.

- [ ] **Step 1: Write failing thumbnail tests**

```csharp
var result = await source.CreateAsync(MultiFileRecord(5), CancellationToken.None);
Assert.AreEqual(3, result.Visuals.Count);
Assert.AreEqual(2, result.RemainingCount);
```

- [ ] **Step 2: Run failing integration test**

Run: `dotnet test tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj --filter "FullyQualifiedName~WindowsShellPreviewSourceTests"`

Expected: compile failure before thumbnail source exists.

- [ ] **Step 3: Implement protected image and Shell file source**

```csharp
public sealed record PreviewThumbnailResult(IReadOnlyList<string> Visuals,
    int RemainingCount, string FallbackKind);
```

Reuse protected image data URLs. For files, use `IShellItemImageFactory` for 320 x 320 previews; encode only in memory, fall back to a registered type icon, return the first three visuals, and cache bounded by canonical path/last-write time/size.

- [ ] **Step 4: Verify cancellation, missing paths, and fallback**

Run: `dotnet test tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj --filter "FullyQualifiedName~WindowsShellPreviewSourceTests"`

- [ ] **Step 5: Commit thumbnail checkpoint**

Commit message: `feat: add safe preview thumbnails`

### Task 4: Add Reusable Native Window and Hover Shortcut Lease

**Files:**
- Create: `src/HuahaiClipboard.App/Presentation/Windows/ContentPreviewWindow.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/ContentPreviewWindow.xaml.cs`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/PreviewWindowCoordinator.cs`
- Modify: `src/HuahaiClipboard.App/Infrastructure/Input/GlobalInputService.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.Core/Services/WebBridgeProtocol.cs`
- Modify: `src/HuahaiClipboard.Core/Services/WebBridgeRequest.cs`
- Create: `tests/HuahaiClipboard.App.Smoke/ContentPreviewWindowSmoke.ps1`
- Create: `tests/HuahaiClipboard.App.Smoke/PreviewShortcutLeaseSmoke.ps1`

**Interfaces:**
- Produces `PreviewWindowCoordinator.OpenAsync(Guid, PreviewOpenSource, CancellationToken)` and preview hotkey lease update behavior.

- [ ] **Step 1: Write failing native smoke**

```powershell
if (-not $result.MainVisible -or $result.WindowsOverlap -or $result.PreviewCount -ne 1) {
    throw 'Right-click preview window contract failed.'
}
```

- [ ] **Step 2: Run native smoke**

Run: `pwsh tests/HuahaiClipboard.App.Smoke/ContentPreviewWindowSmoke.ps1 -Configuration Debug`

Expected: failure before a preview window is implemented.

- [ ] **Step 3: Implement `ContentPreviewWindow` and coordinator**

Use the same `Assets` virtual host and load `product-shell.html?surface=preview`. Configure a borderless, resizable `AppWindow`; restore/save independent placement and topmost state; reuse one live preview; show Save/Discard/Cancel before dirty replacement or close.

```csharp
public enum PreviewOpenSource { RightClick, HoverShortcut }
```

For right click, position `preview.X = main.X - 18 - preview.Width`, clamp into the active work area, and keep the main window visible. Shortcut opens preserve the existing main auto-hide preference. Mouse leave is 250 ms and is canceled for re-entry, editor focus, dirty state, or confirmation.

- [ ] **Step 4: Implement native shortcut lease lifecycle**

Reserve a second `RegisterHotKey` ID. Register only while policy eligibility holds; release on hover leave, virtual row removal, main hide, settings open, deletion, suspend, and disposal. A conflict posts an actionable warning and leaves right-click available.

- [ ] **Step 5: Run smoke and commit**

Run: `pwsh tests/HuahaiClipboard.App.Smoke/ContentPreviewWindowSmoke.ps1 -Configuration Debug`

Run: `pwsh tests/HuahaiClipboard.App.Smoke/PreviewShortcutLeaseSmoke.ps1 -Configuration Debug`

Commit message: `feat: add native preview window and shortcut lease`

### Task 5: Add the Approved Preview Route to the Existing Web Shell

**Files:**
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Create: `tests/HuahaiClipboard.App.Smoke/WebViewPreviewBoardSmoke.cjs`
- Modify: `tests/HuahaiClipboard.App.Smoke/WebViewRecordActionsSmoke.cjs`

**Interfaces:**
- Consumes native `previewState`, `thumbnail`, and `previewError` messages.
- Produces bridge actions `previewReady`, `savePreview`, `discardPreview`, `previewCopy`, `previewPointer`, `previewTopmost`, `previewClose`, and `previewResize`.

- [ ] **Step 1: Write a failing route test**

```javascript
assert.equal(preview.topLeftIconCount, 0);
assert.match(preview.countText, /3,853 字符 · 约 143 行/);
assert.equal(preview.editorScrollable, true);
assert.equal(actions.activePinUse, '#pinSolidPath');
assert.equal(actions.activeFavoriteGlyph, '★');
```

- [ ] **Step 2: Run browser test**

Run: `node tests/HuahaiClipboard.App.Smoke/WebViewPreviewBoardSmoke.cjs`

Expected: failure before `surface=preview` exists.

- [ ] **Step 3: Implement same-shell preview route**

Read `new URLSearchParams(location.search).get('surface')`. Do not alter default main-panel route markup or existing pin/favorite component definitions. For preview route render textual upper-left title, title-bar controls, wrapping editor or read-only path plus editable display name, thumbnails, status/footer, resize affordance, and dirty confirmation.

- [ ] **Step 4: Implement live long-content metrics and control behavior**

```javascript
function previewMetrics(value) {
  const lines = value.split('\n').reduce((sum, line) => sum + Math.max(1, Math.ceil(line.length / 38)), 0);
  return `${value.length.toLocaleString('zh-CN')} 字符 · 约 ${lines} 行`;
}
```

Show formatted length in main metadata for text above 120 characters. Keep all image/file paths read-only; Save sends display name only. Every visible button has an accessible label, tooltip, active/disabled state, and working bridge action.

- [ ] **Step 5: Run Web tests and commit**

Run: `node tests/HuahaiClipboard.App.Smoke/WebViewPreviewBoardSmoke.cjs`

Run: `node tests/HuahaiClipboard.App.Smoke/WebViewRecordActionsSmoke.cjs`

Commit message: `feat: add preview board web route`

### Task 6: Complete Cross-Window Verification

**Files:**
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/ContentPreviewWindow.xaml.cs`
- Create: `tests/HuahaiClipboard.App.Smoke/LongContentPreviewEndToEndSmoke.ps1`
- Create: `tests/HuahaiClipboard.App.Smoke/LongContentPreviewEndToEndSmoke.cjs`
- Modify: `tests/HuahaiClipboard.Core.Tests/UiResourceContractTests.cs`

**Interfaces:**
- Consumes every preceding Core, native, and Web bridge API.
- Produces synchronized main/preview state and deterministic acceptance evidence.

- [ ] **Step 1: Write failing end-to-end probe**

```powershell
if ($result.Passed -ne 22 -or $result.RuntimeErrors -ne 0) {
    throw 'Long-content preview end-to-end contract failed.'
}
```

- [ ] **Step 2: Run end-to-end smoke**

Run: `pwsh tests/HuahaiClipboard.App.Smoke/LongContentPreviewEndToEndSmoke.ps1 -Configuration Debug`

Expected: failure until both WebViews synchronize save, thumbnail, and close state.

- [ ] **Step 3: Implement typed bridge refresh**

Validate every record ID native-side. After save, pin/favorite, delete, or history refresh, update both surfaces. A deleted record keeps its preview draft and Copy action available with a recovery error.

- [ ] **Step 4: Exercise all approved flows**

Verify right-click visibility/no overlap/gap, long text scroll/count, text save, invalid-link conversion, image rename, multi-file stack, dirty guard, 250 ms auto-hide, custom shortcut, drag, resize, topmost, main hide, left-copy hide, and pin/favorite preservation.

- [ ] **Step 5: Run complete verification and commit**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj`

Run: `dotnet test tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj`

Run: `pwsh tests/HuahaiClipboard.App.Smoke/LongContentPreviewEndToEndSmoke.ps1 -Configuration Release`

Run: `dotnet build HuahaiClipboard.sln -c Release -p:Platform=x64`

Save screenshots below `.codex/artifacts/ui-qa/long-content-preview/` without staging them. Commit message: `feat: complete long content preview board`.

## Plan Self-Review

- Tasks 1-2 cover data compatibility, persistence, validation, conversion, and projection refresh.
- Task 3 covers protected image previews, Shell file previews, fallbacks, multi-file limits, and bounded caching.
- Task 4 covers one native preview window, source-aware placement, topmost, sizing, auto-hide, and shortcut lease release paths.
- Task 5 covers the approved same-shell UI, long-text indicators, exact history-action visuals, and browser behavior.
- Task 6 covers cross-window synchronization, focused suites, desktop evidence, and Release build verification.
- Every new type is introduced before a later task consumes it. No task adds a second HTML shell or mutates image/file clipboard payloads.
