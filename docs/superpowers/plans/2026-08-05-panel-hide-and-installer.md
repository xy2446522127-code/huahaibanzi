# Panel Hide Button And Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a toolbar button that hides the panel to the background and ship the verified 50ms-drag build as a friend-ready Windows x64 installer.

**Architecture:** The HTML shell reuses its existing `hidePanel()` function and native `hide` bridge action, so no new native state or persistence is introduced. Packaging uses the known-working regular Release output, which contains `App.xbf` and `HuahaiClipboard.App.pri`, plus official Microsoft runtime prerequisites when required; the custom per-user bootstrapper continues to create shortcuts and the uninstall entry.

**Tech Stack:** HTML/CSS/JavaScript, Node test runner, C#/.NET 8 WinUI 3, Windows PowerShell 5.1, .NET Framework 4.8 bootstrapper, Windows App Runtime 1.7.

## Global Constraints

- The minimize button means “hide to background”; it must not exit the process or stop clipboard monitoring.
- The button is placed immediately before `#settingsButton`, uses the existing `.icon-button` glass style, and has the accessible title `隐藏到后台`.
- Existing right-button double-click, custom shortcut, tray summon, visual theme, opacity, history and settings behavior remain unchanged.
- Panel drag hold duration remains exactly `50ms` with the existing `5px` movement threshold.
- Installation is per-user at `%LOCALAPPDATA%\Programs\HuahaiClipboard`; uninstall preserves `%LOCALAPPDATA%\HuahaiClipboard`.
- The release target is Windows 10/11 x64. No Git push or code signing is authorized.

---

### Task 1: Toolbar Hide Button

**Files:**
- Modify: `tests/PrototypeShellContractTests.cjs`
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`

**Interfaces:**
- Consumes: existing JavaScript `hidePanel(): void` and native bridge action `hide`.
- Produces: toolbar control `button#minimizeButton` with `title="隐藏到后台"` and click binding `hidePanel`.

- [ ] **Step 1: Write the failing UI contract test**

Add a production-shell test that loads `product-shell.html`, asserts the toolbar order is `searchInput`, `minimizeButton`, `settingsButton`, and asserts the module contains `hhQ('#minimizeButton').onclick=hidePanel`.

```js
test('production shell exposes a hide-to-background button beside settings', () => {
  const html = fs.readFileSync('src/HuahaiClipboard.App/Assets/Web/product-shell.html', 'utf8');
  const toolbar = html.match(/<div class="toolbar">([\s\S]*?)<\/div>\s*<div class="filters">/)[1];
  assert.ok(toolbar.indexOf('id="minimizeButton"') < toolbar.indexOf('id="settingsButton"'));
  assert.match(toolbar, /id="minimizeButton"[^>]*title="隐藏到后台"/);
  assert.match(html, /hhQ\('#minimizeButton'\)\.onclick=hidePanel/);
});
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: `node --test tests/PrototypeShellContractTests.cjs`

Expected: FAIL because `minimizeButton` is absent.

- [ ] **Step 3: Add the minimal production control**

Place this button directly before the existing settings button and bind it with the other toolbar handlers:

```html
<button class="icon-button" id="minimizeButton" title="隐藏到后台">−</button>
```

```js
hhQ('#minimizeButton').onclick=hidePanel;
```

- [ ] **Step 4: Verify GREEN and direct regressions**

Run: `node --test tests/PrototypeShellContractTests.cjs tests/PanelDragPolicyTests.cjs`

Expected: all tests pass; the existing interactive-target selector keeps the button from arming drag.

- [ ] **Step 5: Create a focused checkpoint**

Checkpoint only the test and HTML paths with subject `feat: add hide-to-background toolbar button`.

### Task 2: Friend-Ready Installer

**Files:**
- Modify: `installer/Bootstrapper.cs`
- Modify: `installer/Build-Installer.ps1`
- Create: `installer/Fetch-Prerequisites.ps1`
- Verify: `installer/Uninstall.ps1`

**Interfaces:**
- Consumes: the regular x64 Release directory containing `HuahaiClipboard.App.exe`, `App.xbf`, `HuahaiClipboard.App.pri`, `Assets/Web/product-shell.html`, and `Assets/Web/panel-drag.js`.
- Produces: `dist/HuahaiClipboard-Setup.exe`, installed `HuahaiClipboard.exe`, desktop/start-menu shortcuts, and the HKCU uninstall entry.

- [ ] **Step 1: Add package-layout failure checks**

Update `Build-Installer.ps1` validation to reject input without both required WinUI resources and the 50ms script:

```powershell
$required = @('HuahaiClipboard.App.exe', 'App.xbf', 'HuahaiClipboard.App.pri', 'Assets\Web\product-shell.html', 'Assets\Web\panel-drag.js')
foreach ($relativePath in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot $relativePath))) {
        throw "Release directory is incomplete: $relativePath"
    }
}
```

- [ ] **Step 2: Verify the bad publish layout is rejected**

Run the build script against `dist/release-1.0.0-50ms-framework-wasdk/app`.

Expected: FAIL with `App.xbf` missing, reproducing the cause of exit code `0xc000027b` before compilation.

- [ ] **Step 3: Fetch only official runtime prerequisites**

Create `Fetch-Prerequisites.ps1` to download the current .NET 8 x64 Windows Desktop Runtime using Microsoft release metadata with SHA-512 verification, and Windows App Runtime 1.7 x64 from Microsoft's official distribution URL. Store downloads under ignored `dist/prerequisites`; do not install them on the development machine.

- [ ] **Step 4: Bundle and conditionally install prerequisites**

Copy the prerequisite installers into `payload/prerequisites`. Before starting the app, `Bootstrapper.cs` checks for `Microsoft.WindowsDesktop.App` major version 8 and Windows App Runtime 1.7 x64; it runs only the missing official installer, accepts exit codes `0`, `1638`, and `3010`, and treats cancellation or any other exit code as an installation failure. Remove prerequisite files from the final application directory after successful setup.

- [ ] **Step 5: Build from the known-working Release layout**

Run:

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' `
  'src\HuahaiClipboard.App\HuahaiClipboard.App.csproj' /restore /t:Build `
  /p:Configuration=Release /p:Platform=x64 /m /nologo /v:minimal

& 'installer\Build-Installer.ps1' `
  -PublishRoot 'src\HuahaiClipboard.App\bin\x64\Release\net8.0-windows10.0.19041.0' `
  -OutputPath 'dist\HuahaiClipboard-Setup.exe'
```

Expected: setup compilation succeeds, preserves `App.xbf/.pri`, renames only the verified apphost to `HuahaiClipboard.exe`, and embeds the fox icon.

- [ ] **Step 6: Run the full release verification**

Run all Node tests, `dotnet test` for Core, Release x64 build, installer SHA-256/signature inspection, silent install, shortcut/registry checks, installed startup smoke, silent uninstall, data-directory preservation check, final reinstall and background startup check.

Expected: no Node/Core/build failures; installed app shows a stable top-level window; uninstall removes program files and shortcuts while preserving user data; final reinstall leaves the latest app available.

- [ ] **Step 7: Independent release review and handoff**

Dispatch one read-only reviewer with the approved spec, affected diff and verification evidence. Fix all Critical/Important findings, rerun affected verification, then report the absolute setup path, size, SHA-256, unsigned SmartScreen limitation, install location and preserved data location.
