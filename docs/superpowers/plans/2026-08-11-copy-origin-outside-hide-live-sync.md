# Copy Origin, Outside Hide, and Live Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop panel-originated clipboard writes from returning to history, add a persisted outside-click hide setting, and make hidden-period clipboard changes visible on the first summoned frame.

**Architecture:** A process-random marker plus Windows clipboard sequence number identifies owned writes without a timing heuristic. A latest-only async refresh coordinator coalesces background changes, while an async visibility controller resumes content and applies the newest state before showing the window. The approved `product-shell.html` remains the sole UI source.

**Tech Stack:** .NET 8, WinUI 3, WebView2, Windows Forms clipboard APIs, MSTest, HTML/CSS/JavaScript.

## Global Constraints

- Preserve Windows 10/11 x64 support and the current WebView2 UI carrier.
- `HideOnOutsideClick` is Boolean, defaults to `true`, and persists locally.
- Panel-originated copy never changes the source record or existing history.
- Existing duplicate records are not migrated or deleted.
- Keep hidden WebView2 suspension and low-idle-memory behavior.
- Push verified source to the existing Git remote; do not install or update the local installed client.

---

### Task 1: Clipboard write-origin protocol

**Files:**
- Create: `src/HuahaiClipboard.Core/Contracts/IClipboardWriteOriginGuard.cs`
- Create: `src/HuahaiClipboard.Core/Services/ClipboardWriteOriginState.cs`
- Create: `src/HuahaiClipboard.App/Infrastructure/Clipboard/WindowsClipboardWriteOriginGuard.cs`
- Modify: `src/HuahaiClipboard.App/Infrastructure/Clipboard/WindowsClipboardPlatform.cs`
- Modify: `src/HuahaiClipboard.App/Infrastructure/Clipboard/ClipboardCaptureService.cs`
- Modify: `src/HuahaiClipboard.App/CompositionRoot.cs`
- Create: `tests/HuahaiClipboard.Core.Tests/ClipboardWriteOriginStateTests.cs`
- Create: `tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj`
- Create: `tests/HuahaiClipboard.App.IntegrationTests/ClipboardCaptureServiceTests.cs`
- Modify: `HuahaiClipboard.sln`

**Interfaces:**
- Produces: `IClipboardWriteOriginGuard.AttachMarker(IDataObject)`, `RecordSuccessfulWrite()`, and `IsCurrentWrite()`.
- Produces: `ClipboardWriteOriginState.Record(uint)` and `Matches(string?, uint)`.

- [ ] **Step 1: Write failing state and capture tests**

  Add tests proving marker plus sequence is required, a changed sequence is external, and `ClipboardCaptureService` performs no history mutation when the guard identifies an owned write.

- [ ] **Step 2: Run the focused tests and verify RED**

  Run `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj -c Release --filter ClipboardWriteOriginStateTests` and the new Windows integration test project. Expected failure: missing origin types and constructor dependency.

- [ ] **Step 3: Implement the minimal protocol**

  Build one `DataObject` per write, attach the private marker before `SetDataObject`, and record the sequence only after successful clipboard ownership. Check the guard before decoding clipboard content.

- [ ] **Step 4: Run focused and direct-regression tests GREEN**

  Re-run the new tests plus `ProductionClipboardTests`; require zero failures.

### Task 2: Persisted outside-click hiding

**Files:**
- Modify: `src/HuahaiClipboard.Core/Settings/BehaviorSettings.cs`
- Modify: `src/HuahaiClipboard.Core/Services/WebBridgeProtocol.cs`
- Modify: `src/HuahaiClipboard.Core/Services/TransientWindowVisibilityController.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Modify: `tests/HuahaiClipboard.Core.Tests/JsonSettingsStoreTests.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/TransientWindowVisibilityControllerTests.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/UiResourceContractTests.cs`

**Interfaces:**
- Produces: `BehaviorSettings.HideOnOutsideClick` with default `true`.
- Produces: bridge action `setOutsideAutoHide` and native state field `hideOnOutsideClick`.
- Produces: `TransientWindowVisibilityController.HideOnDeactivated(bool enabled, bool interactionActive)`.

- [ ] **Step 1: Write failing persistence, policy, bridge, and UI contract tests**

  Prove old settings default to enabled, an explicit disabled value round-trips, disabled or active manipulation prevents deactivation hiding, and the visible control is bound to the bridge action.

- [ ] **Step 2: Run the focused tests and verify RED**

  Run Core tests filtered to the three affected classes. Expected failure: missing property, action, controller method, and DOM control.

- [ ] **Step 3: Implement the setting and native deactivation route**

  Add the setting row using existing `setting-row` and `toggle` classes. Save through `SettingsViewModel`; hide only when enabled and no drag/scale interaction is active.

- [ ] **Step 4: Run focused tests GREEN**

  Re-run persistence, visibility, bridge, and resource contract tests.

### Task 3: Latest-only hidden history synchronization

**Files:**
- Create: `src/HuahaiClipboard.Core/Services/LatestOnlyAsyncRefresh.cs`
- Modify: `src/HuahaiClipboard.Core/Services/TransientWindowVisibilityController.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Create: `tests/HuahaiClipboard.Core.Tests/LatestOnlyAsyncRefreshTests.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/TransientWindowVisibilityControllerTests.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs`

**Interfaces:**
- Produces: `LatestOnlyAsyncRefresh.RequestAsync()` and `FlushAsync()`.
- Produces: `TransientWindowVisibilityController.ShowAsync(Func<Task> synchronizeBeforeShow)`.
- Produces: browser callable `window.HuahaiApplyNativeState(state)`.

- [ ] **Step 1: Write failing coalescing and show-order tests**

  Block the first refresh, request ten changes, and prove no concurrent execution while the final requested revision completes. Prove window `Show` occurs only after content activation and synchronization.

- [ ] **Step 2: Run focused tests and verify RED**

  Run tests filtered to `LatestOnlyAsyncRefreshTests|TransientWindowVisibilityControllerTests|ShellIntegrationPolicyTests`. Expected failure: missing coordinator and async show contract.

- [ ] **Step 3: Implement coalesced refresh and pre-show DOM apply**

  Replace per-event full-state posting with latest-only refresh. Skip Web posting while hidden; on summon flush, resume, invoke `HuahaiApplyNativeState` through `ExecuteScriptAsync`, then show.

- [ ] **Step 4: Run focused and stress tests GREEN**

  Repeat the ten-change scenario at least 50 times and require no stale final revision, concurrent refresh, deadlock, or ordering failure.

### Task 4: Integrated user journeys and delivery

**Files:**
- Modify: `.codex/app-product-delivery-interaction-contract.json` only if the existing contract requires the new control to be registered.
- Create: `.codex/app-product-delivery-feature-index.json` after the verified functional checkpoint if required by the delivery policy.
- Create: `.codex/app-product-delivery-decision-registry.json` with the confirmed product decisions and incident evidence.
- Modify: `README.md` only when user-visible behavior documentation is missing.

**Interfaces:**
- Consumes all Task 1–3 behavior.
- Produces reproducible validation commands and a pushed Git commit.

- [ ] **Step 1: Run browser interaction evidence**

  Exercise the real `product-shell.html`: open System settings, toggle outside hide, click a record, summon again, and verify the record count and order remain unchanged with zero console errors.

- [ ] **Step 2: Run Windows-path simulations**

  Execute owned-write suppression for text/file/image data-object construction, disabled/enabled deactivation paths, and hidden ten-copy summon ordering against real production classes with only OS clipboard mutation mocked.

- [ ] **Step 3: Run full regression and Release x64 build**

  Run all solution test projects, Node/Web tests declared by the repository, the interaction inventory, and the documented MSBuild Release x64 command without installation.

- [ ] **Step 4: Review privacy and Git scope**

  Confirm no clipboard payload, local `Data`, browser profile, certificate, runtime log, screenshot, installer, or pre-existing untracked file is staged.

- [ ] **Step 5: Checkpoint and push**

  Create one verified functional checkpoint containing only owned paths, push `codex/native-ui-spike` to `origin`, and verify local/remote commit hashes match. Do not create or install a local update.
