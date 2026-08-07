# Restore Approved WebView UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the exact approved 1.0.0 WebView UI as the 1.1.1 production desktop surface while retaining all verified clipboard and OS integration behavior.

**Architecture:** Make `HuahaiClipboard.App` the release entry again and load the preserved offline `product-shell.html` through WebView2. Keep Core services and the real native bridge as the behavior source; retain the WPF port only as a rollback implementation.

**Tech Stack:** .NET 8, WinUI 3, WebView2, Windows App SDK, HTML/CSS/JavaScript, PowerShell installer, MSTest, Node test runner.

## Global Constraints

- The preserved 1.0.0 WebView shell is the only production visual source.
- Existing local data, clipboard semantics, privacy filters and cleanup rules remain compatible.
- The restored desktop build loads only packaged offline UI and has no development HTTP-origin or network UI dependency.
- WebView memory regression is an explicitly accepted trade-off.
- The release must install to `F:\HuahaiClipboard` without deleting user data.
- One writer, no worktree, no subagent, rollback commit `42af42a329c94965ebb0f1ac811c938265f8b745`.

---

### Task 1: Lock the preserved UI carrier

**Files:**
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Modify: `tests/PrototypeShellContractTests.cjs`
- Modify: `.codex/app-product-delivery-ui-carrier.json`
- Modify: `.codex/app-product-delivery-visual-source.json`

**Interfaces:**
- Consumes: preserved release shell SHA-256 and approved screenshots.
- Produces: one versioned WebView source manifest used by preview and installed desktop.

- [ ] Add a failing contract assertion for the preserved header, old DOM hierarchy, dimensions, settings navigation and visual tokens.
- [ ] Run `node --test tests/PrototypeShellContractTests.cjs` and confirm the current divergent shell fails.
- [ ] Restore the preserved shell, then merge only required 1.1.1 bridge hooks without changing its structure or visual CSS.
- [ ] Re-run the focused Node test and all `tests/*.cjs`.

### Task 2: Restore the production WebView host

**Files:**
- Modify: `src/HuahaiClipboard.App/App.xaml.cs`
- Modify: `src/HuahaiClipboard.App/CompositionRoot.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/HuahaiClipboard.App.csproj`
- Test: `tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs`

**Interfaces:**
- Consumes: `WebBridgeProtocol`, production history/settings sources and global input service.
- Produces: offline WebView lifecycle with `ShowAtCursor`, `HidePanelWindow`, `PushStateToWebAsync` and real bridge actions.

- [ ] Add failing tests proving release state, live count, hide, Topmost and every approved bridge action are reachable.
- [ ] Run the focused Core tests and confirm the missing/incorrect release-host behavior fails.
- [ ] Make the formal WebView host the production entry and bind every action to existing production services.
- [ ] Build `HuahaiClipboard.App` and run Core plus WebView smoke tests.

### Task 3: Rebuild release packaging for WebView

**Files:**
- Modify: `installer/Build-Installer.ps1`
- Modify: `installer/Bootstrapper.cs`
- Modify: `installer/PrerequisitePolicy.cs`
- Modify: `installer/Fetch-Prerequisites.ps1`
- Test: `tests/InstallerPackagePolicyTests.ps1`
- Test: `tests/InstallerPrerequisitePolicyTests.ps1`

**Interfaces:**
- Consumes: published `HuahaiClipboard.App` output and prerequisite manifest.
- Produces: `dist/HuahaiClipboard-Setup.exe` version 1.1.1.

- [ ] Add failing installer policy assertions requiring the WebView shell, XBF/PRI resources, WebView2 loader and Windows App Runtime payload.
- [ ] Run installer policy tests and confirm the native-only package fails.
- [ ] Restore WebView package validation and bootstrap prerequisites while preserving non-C install selection and uninstall policy.
- [ ] Publish the formal app and build the 1.1.1 setup executable.

### Task 4: Visual and installed acceptance

**Files:**
- Create: `.codex/artifacts/ui-qa/webview-restore/restored-panel.png` (ignored)
- Create: `.codex/artifacts/ui-qa/webview-restore/restored-settings.png` (ignored)
- Create: `.codex/artifacts/ui-qa/webview-restore/panel-diff.png` (ignored)
- Modify: `.codex/app-product-delivery-interaction-contract.json`

**Interfaces:**
- Consumes: signed-off screenshots and 1.1.1 installer.
- Produces: visual diff, dynamic interaction evidence and installed smoke results.

- [ ] Capture the restored panel and settings at the baseline viewport and fixed fixture state.
- [ ] Compute a local pixel/layout diff and reject structural mismatches.
- [ ] Exercise every visible control and record `missing=0`, `unexplained=0`, no console errors.
- [ ] Snapshot user-data hashes, install 1.1.1 over `F:\HuahaiClipboard`, and prove hashes remain unchanged.
- [ ] Run real clipboard, live count, Topmost, hide-to-background, tray and shortcut smoke tests.

### Task 5: Release and Git synchronization

**Files:**
- Modify: `README.md`
- Modify: `.codex/app-product-delivery-progress.json`
- Modify: `.codex/app-product-delivery-ui-carrier.json`

**Interfaces:**
- Consumes: final build, test, install and visual evidence.
- Produces: verified release checkpoint and synchronized public `master`.

- [ ] Run Core, Web, installer and installed-app test matrices once.
- [ ] Run the release delivery gate, diff review, secret scan and private-artifact scan.
- [ ] Create one verified checkpoint with only owned product paths.
- [ ] Fast-forward `master`, push without force, and verify local/remote SHA equality.
- [ ] Start the installed app in background and verify the WinUI resource-preserving process path is `F:\HuahaiClipboard\HuahaiClipboard.App.exe`.
