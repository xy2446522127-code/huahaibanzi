# Clipboard Record Remarks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a persistent short remark to every clipboard history record through the real right-click preview and display it in the existing main-panel metadata line.

**Architecture:** Extend the immutable `ClipboardRecord` with an optional trailing `Remark`, carry it through `PreviewEdit`, encrypted JSON persistence, the WebView bridge, and the existing product shell. Keep generated display detail separate from the remark so paths, dimensions, timestamps, and source processes remain intact. The existing todo workspace is only covered by regression verification.

**Tech Stack:** .NET 8, C#, MSTest, WinUI 3, WebView2, vanilla JavaScript, protected JSON history.

## Global Constraints

- Base all source changes on GitHub `origin/master` commit `d36e75d` plus the local committed design document `5e4dd3e`.
- Do not change the todo workspace, capsule behavior, note editor, installer, version, package, or GitHub Release.
- `Remark` is a nullable, trimmed, single-line value with a maximum of 100 characters; an empty input persists as `null`.
- Existing encrypted history written before this feature must deserialize without migration or data loss.
- Re-copying the same clipboard payload must retain its existing remark.
- Use `apply_patch` for source and test edits; stage only files owned by each task.

---

### Task 1: Model, edit policy, and protected-history compatibility

**Files:**
- Modify: `src/HuahaiClipboard.Core/Models/ClipboardRecord.cs`
- Modify: `src/HuahaiClipboard.Core/Models/PreviewEdit.cs`
- Modify: `src/HuahaiClipboard.Core/Services/ClipboardRecordEditor.cs`
- Modify: `src/HuahaiClipboard.Core/Services/JsonClipboardHistorySource.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/ClipboardRecordEditorTests.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/ClipboardRecordDisplayTests.cs`

**Interfaces:**
- Consumes: `PreviewEdit(ClipboardItemKind ExpectedKind, string Value)` and `ClipboardRecord` from existing callers.
- Produces: `ClipboardRecord.Remark` and `PreviewEdit.Remark`; `ClipboardRecordEditor.Apply` returns a record with normalized remark or a validation error.

- [ ] **Step 1: Write failing editor and persistence tests**

```csharp
[TestMethod]
public void PreviewEdit_NormalizesRemarkWithoutChangingFilePayload()
{
    var result = ClipboardRecordEditor.Apply(
        CreateRecord(ClipboardItemKind.File, @"F:\资料\发布计划.docx"),
        new PreviewEdit(ClipboardItemKind.File, "发布计划", "  发布前确认  "));

    Assert.IsTrue(result.Succeeded, result.ErrorMessage);
    Assert.AreEqual("发布前确认", result.Record!.Remark);
    Assert.AreEqual(@"F:\资料\发布计划.docx", result.Record.PrimaryText);
}

[TestMethod]
public void PreviewEdit_RejectsMultiLineOrOverlongRemark()
{
    var multiLine = ClipboardRecordEditor.Apply(
        CreateRecord(ClipboardItemKind.Text, "内容"),
        new PreviewEdit(ClipboardItemKind.Text, "内容", "第一行\n第二行"));
    var overlong = ClipboardRecordEditor.Apply(
        CreateRecord(ClipboardItemKind.Text, "内容"),
        new PreviewEdit(ClipboardItemKind.Text, "内容", new string('备', 101)));

    Assert.IsFalse(multiLine.Succeeded);
    Assert.IsFalse(overlong.Succeeded);
}
```

- [ ] **Step 2: Run the focused tests and confirm the new constructor/field assertions fail**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests --filter FullyQualifiedName~ClipboardRecordEditorTests`

Expected: FAIL because `PreviewEdit` has no remark parameter and `ClipboardRecord` has no remark state.

- [ ] **Step 3: Add the model fields and central normalization**

```csharp
public sealed record ClipboardRecord(
    Guid Id, ClipboardItemKind Kind, string PrimaryText, string SecondaryText,
    DateTimeOffset LastCopiedAt, bool IsFavorite, bool IsPinned, bool IsAvailable,
    string? PreviewAssetPath, string? SourcePath = null, string? DisplayName = null,
    string? Remark = null);

public sealed record PreviewEdit(ClipboardItemKind ExpectedKind, string Value, string? Remark = null);

private static string? NormalizeRemark(string? value)
{
    var normalized = value?.Trim();
    if (string.IsNullOrEmpty(normalized)) return null;
    if (normalized.Length > 100 || normalized.Contains('\r') || normalized.Contains('\n'))
        throw new ArgumentException("备注最多 100 个字符且不能换行");
    return normalized;
}
```

Apply normalization before the record-kind switch and translate its validation failure into `PreviewEditResult.ValidationError`. Every successful record branch uses `record with { Remark = normalized }` alongside its existing content mutation.

- [ ] **Step 4: Preserve the field through same-payload upsert and old JSON reads**

```csharp
values[index] = record with
{
    Id = existing.Id,
    IsFavorite = existing.IsFavorite,
    IsPinned = existing.IsPinned,
    Remark = existing.Remark
};
```

Add tests that deserialize JSON without the `Remark` property, save a remark, reload it, and upsert an equivalent payload without losing it.

- [ ] **Step 5: Run the focused Core tests**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests --filter "FullyQualifiedName~ClipboardRecordEditorTests|FullyQualifiedName~ClipboardRecordDisplayTests"`

Expected: PASS, including old JSON compatibility, rejected invalid remarks, empty-to-null clearing, and repeated capture retention.

- [ ] **Step 6: Commit the isolated data-policy change**

```powershell
git add -- src/HuahaiClipboard.Core/Models/ClipboardRecord.cs src/HuahaiClipboard.Core/Models/PreviewEdit.cs src/HuahaiClipboard.Core/Services/ClipboardRecordEditor.cs src/HuahaiClipboard.Core/Services/JsonClipboardHistorySource.cs tests/HuahaiClipboard.Core.Tests/ClipboardRecordEditorTests.cs tests/HuahaiClipboard.Core.Tests/ClipboardRecordDisplayTests.cs
git commit -m "feat: persist remarks on clipboard records"
```

### Task 2: View-model search and WebView contract

**Files:**
- Modify: `src/HuahaiClipboard.Core/Presentation/PanelViewModel.cs`
- Modify: `src/HuahaiClipboard.Core/Services/WebBridgeRequest.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/ContentPreviewWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/PanelViewModelTests.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs`

**Interfaces:**
- Consumes: `ClipboardRecord.Remark`, `PreviewEdit.Remark`, and the `savePreview` WebView message.
- Produces: `WebBridgeRequest.Remark`, preview state `record.remark`, main panel state `remark`, and search results that include remarks.

- [ ] **Step 1: Write failing search and bridge parsing tests**

```csharp
[TestMethod]
public async Task Search_FindsRecordByRemark()
{
    var record = Record("正文") with { Remark = "发布前检查" };
    var viewModel = CreateViewModel(record);
    await viewModel.LoadAsync();
    viewModel.SearchText = "检查";

    Assert.AreEqual(record.Id, viewModel.VisibleRecords.Single().Id);
}

[TestMethod]
public void WebBridgeRequest_ParsesPreviewRemark()
{
    var parsed = WebBridgeRequest.TryParse(
        "{\"action\":\"savePreview\",\"id\":\"00000000-0000-0000-0000-000000000001\",\"text\":\"正文\",\"remark\":\"发布前检查\"}",
        out var request);

    Assert.IsTrue(parsed);
    Assert.AreEqual("发布前检查", request!.Remark);
}
```

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests --filter "FullyQualifiedName~PanelViewModelTests|FullyQualifiedName~ShellIntegrationPolicyTests"`

Expected: FAIL because search and `WebBridgeRequest` do not expose remarks.

- [ ] **Step 3: Extend search, bridge parsing, and state serialization**

```csharp
private static bool MatchesSearch(ClipboardRecord record, string query) =>
    record.PrimaryText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
    record.SecondaryText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
    record.DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
    record.SourcePath?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
    record.Remark?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
```

Add `string? Remark` to `WebBridgeRequest`, parse `remark`, construct `new PreviewEdit(record.Kind, request.Text ?? string.Empty, request.Remark)`, emit `remark = record.Remark` from both preview state and main-panel record state.

- [ ] **Step 4: Run focused Core and bridge tests**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests --filter "FullyQualifiedName~PanelViewModelTests|FullyQualifiedName~ShellIntegrationPolicyTests"`

Expected: PASS, with unchanged unsupported-action policy and a parsed independent `remark` field.

- [ ] **Step 5: Commit the state-contract change**

```powershell
git add -- src/HuahaiClipboard.Core/Presentation/PanelViewModel.cs src/HuahaiClipboard.Core/Services/WebBridgeRequest.cs src/HuahaiClipboard.App/Presentation/Windows/ContentPreviewWindow.xaml.cs src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs tests/HuahaiClipboard.Core.Tests/PanelViewModelTests.cs tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs
git commit -m "feat: bridge clipboard record remarks"
```

### Task 3: Real preview and main-panel Web UI

**Files:**
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Modify: `tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs`

**Interfaces:**
- Consumes: preview `record.remark`, main-panel `item.remark`, and `savePreview` with `{ id, text, remark, mode }`.
- Produces: a single-line remark editor in the existing preview surface and the main panel metadata sequence `置顶 · 备注 · 原有 meta · 字符数`.

- [ ] **Step 1: Write a failing source-contract test for the real shell**

```csharp
[TestMethod]
public void ProductShell_PreservesRemarkAcrossPreviewSaveAndMetadataRender()
{
    var shell = File.ReadAllText(ProductShellPath);

    StringAssert.Contains(shell, "id=\"previewRemark\"");
    StringAssert.Contains(shell, "remark:remark.value");
    StringAssert.Contains(shell, "item.remark");
}
```

- [ ] **Step 2: Run the shell policy test and confirm it fails**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests --filter FullyQualifiedName~ShellIntegrationPolicyTests`

Expected: FAIL because the actual shell has neither a remark editor nor a remark metadata token.

- [ ] **Step 3: Add the preview remark control and unified dirty/save behavior**

In the `surface=preview` branch, render a labeled `<input id="previewRemark" maxlength="100">` below the existing content editor. Reuse preview input styling with a compact glass row, a live `0 / 100` counter, an icon-only clear button with `title="清空备注"`, and no new window or navigation.

```javascript
const remark = hhQ('#previewRemark');
const draftChanged = () => editor.value !== value || remark.value !== (record.remark || '');
const savePreview = mode => postNative('savePreview', {
  id: record.id, text: editor.value, remark: remark.value, ...(mode ? {mode} : {})
});
```

Call `draftChanged` for `previewDirty`, pass the same fields from the normal save, Ctrl+S, and unsaved-confirmation save paths. Do not interpolate raw user input; retain the existing `safe` escaping when rendering initial values.

- [ ] **Step 4: Compose list metadata without overwriting `meta`**

```javascript
function recordMetadata(item) {
  const length = item.text.length > 120 ? ` · ${item.text.length.toLocaleString('zh-CN')} 字符` : '';
  return [item.pin ? '置顶' : '', item.remark || '', item.meta, length.replace(/^ · /, '')]
    .filter(Boolean)
    .join(' · ');
}
```

Retain the existing `<small>` one-line overflow CSS and add `title="${escapeHtml(recordMetadata(item))}"` so a truncated remark remains readable without adding a third line.

- [ ] **Step 5: Run shell contract tests**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests --filter FullyQualifiedName~ShellIntegrationPolicyTests`

Expected: PASS, proving the checked-in product shell carries the actual preview input, bridge payload, dirty flow, and metadata composition.

- [ ] **Step 6: Commit the real WebView UI change**

```powershell
git add -- src/HuahaiClipboard.App/Assets/Web/product-shell.html tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs
git commit -m "feat: edit and display clipboard remarks"
```

### Task 4: Build, real desktop workflow, and todo regression

**Files:**
- Modify: no source files unless a verification failure identifies a scoped defect.
- Evidence: `.codex/artifacts/ui-qa/clipboard-remarks/` (do not stage)

**Interfaces:**
- Consumes: all completed tasks and the existing `TodoWorkspaceWindow` protocol.
- Produces: proof that the actual WinUI/WebView flow persists a remark while the existing todo workspace remains usable.

- [ ] **Step 1: Run the complete automated suite**

Run: `dotnet test HuahaiClipboard.sln --no-restore`

Expected: PASS with no test failures.

- [ ] **Step 2: Build the real desktop application**

Run: `dotnet build HuahaiClipboard.sln --no-restore -c Release`

Expected: 0 errors; record warning count separately if nonzero.

- [ ] **Step 3: Run the desktop workflow against isolated local data**

Launch the Release application with an isolated app-data root. Capture before/after screenshots and verify this exact sequence: right-click a text record, set `发布前确认`, save, observe `发布前确认 · 相对时间 · 来源`; reopen and clear the field, save, observe no doubled separator; restart and confirm a newly saved remark remains.

- [ ] **Step 4: Verify existing todo behavior in the same build**

Open the existing todo entry from the main panel; add two todos, drag one to a new position, toggle capsule mode, create a note, paste an image into the note, resize the note editor, and reopen the workspace. Record pass/fail evidence only; do not alter todo source unless the remark change caused the regression.

- [ ] **Step 5: Run final repository checks and commit any scoped verification fix**

Run: `git diff --check` and `git status --short`.

Expected: no whitespace errors; only owned feature commits and untracked UI evidence outside Git. If no verification-driven source fix was needed, do not create an empty commit.
