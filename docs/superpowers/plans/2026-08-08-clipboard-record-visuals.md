# Clipboard Record Visuals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship readable file/image rows, lazy `1:1` image previews, and the approved D ruby pin material without changing clipboard paste behavior.

**Architecture:** Keep `PrimaryText` authoritative for paste. Add one optional image-source path, derive UI-only text through a Core projection, and move protected image bytes through a lazy native bridge action. Update the one approved WebView shell rather than creating another UI.

**Tech Stack:** .NET 8, WinUI 3, WebView2, HTML/CSS/JavaScript, MSTest, Node contract tests.

## Global Constraints

- Windows 10/11 x64; no new package dependency.
- Preserve encrypted local storage and all clipboard payloads.
- Preserve the approved `product-shell.html` layout, scale, drag, glass and interaction behavior.
- Do not modify installer, version, update, Git remote or installed application in this stage.

---

### Task 1: Separate display projection from paste payload

**Files:**
- Modify: `src/HuahaiClipboard.Core/Models/ClipboardRecord.cs`
- Create: `src/HuahaiClipboard.Core/Services/ClipboardRecordDisplay.cs`
- Modify: `src/HuahaiClipboard.Core/Presentation/PanelViewModel.cs`
- Modify: `src/HuahaiClipboard.App/Infrastructure/Clipboard/ClipboardCaptureService.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/ClipboardRecordDisplayTests.cs`

- [ ] Write failing tests for one file, multiple files, image-file source path, bitmap fallback, old JSON and source-path search.
- [ ] Run the focused tests and confirm they fail because `SourcePath` and `ClipboardRecordDisplay` do not exist.
- [ ] Add optional `SourcePath`, the display projection, capture assignment and search consumer.
- [ ] Run focused tests and the existing clipboard/history tests.

### Task 2: Add lazy protected image previews

**Files:**
- Create: `src/HuahaiClipboard.Core/Services/ClipboardImagePreviewSourceService.cs`
- Modify: `src/HuahaiClipboard.Core/Services/WebBridgeProtocol.cs`
- Modify: `src/HuahaiClipboard.App/CompositionRoot.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/ClipboardImagePreviewSourceServiceTests.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs`

- [ ] Write failing tests for PNG data URL success, non-image no-read behavior, unavailable fallback and `requestThumbnail` protocol support.
- [ ] Run focused tests and confirm the missing service/action failures.
- [ ] Implement the preview service and native request/reply bridge without exposing `PreviewAssetPath`.
- [ ] Run focused and direct clipboard regressions.

### Task 3: Render thumbnails and approved ruby pin in the single UI shell

**Files:**
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Modify: `tests/PrototypeShellContractTests.cjs`
- Modify: `tests/HuahaiClipboard.App.Smoke/WebViewRecordActionsSmoke.cjs`

- [ ] Add failing contract assertions for exact E718 path IDs, ruby solid pin only, transparent action background, `33px` square thumbnail, cover crop, fallback and lazy request.
- [ ] Run Node tests and confirm the new assertions fail against the old shell.
- [ ] Add the approved SVG definitions, glyph-only pin/star states, thumbnail slot, lazy observer and thumbnail response handling.
- [ ] Run Node contract and WebView record-action tests.

### Task 4: Integrated verification and checkpoint

**Files:**
- Update: `.codex/app-product-delivery-progress.json`
- Include: the files owned by Tasks 1–3 and these approved design/plan documents.

- [x] Run Core tests, affected Node tests, UI carrier/interaction gates and x64 Release build.
- [x] Inspect the approved image-row and D-pin contracts against the preserved UI carrier.
- [x] Review the diff and scan owned files for private artifacts or unrelated changes.
- [x] Create a verified functional Git checkpoint and publish the authorized GitHub update without installing locally.
