# Update Banner and Minimize Icon v1.1.8 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship v1.1.8 with a non-overlapping high-transparency update banner on the main panel and the approved larger rounded minimize glyph.

**Architecture:** Keep the native update coordinator, tray integration, installer and WebView bridge unchanged. Extend the approved `product-shell.html` carrier so the existing `updateStatus` message drives the badge, banner and About page from one state source; reuse existing `snoozeUpdate`, `installUpdate`, `hide`, and settings routing actions.

**Tech Stack:** WinUI 3, WebView2, HTML/CSS/JavaScript, .NET 8, Node test runner, PowerShell installer tests, GitHub Releases.

## Global Constraints

- Windows 10 / 11 x64 only; no new runtime or service dependency.
- Preserve the approved rose-purple liquid-glass UI carrier and all existing clipboard behavior.
- The banner has no leading icon and must not overlap at 80%, 100%, or 160% scale.
- Glass background becomes more transparent while text and controls remain fully legible.
- The minimize action hides to the background and never exits the process.
- Client updates are triggered only by a higher-version public GitHub Release containing `HuahaiClipboard-Setup.exe`.
- Clipboard records, settings and user data remain local and must not enter Git or release assets.

---

### Task 1: Main-panel update banner and minimize glyph

**Files:**
- Modify: `tests/CompletePrototypeExperienceTests.cjs`
- Modify: `tests/PrototypeShellContractTests.cjs`
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`

**Interfaces:**
- Consumes: native `updateStatus` fields `updateAvailable`, `latestVersion`, `canInstall`, `notifyUser`, `status`, `message`, and `progress`.
- Produces: `#updateBanner`, `#updateBannerVersion`, `#updateBannerLater`, `#updateBannerInstall`, inline `.minimize-glyph`, and existing bridge actions `snoozeUpdate`, `installUpdate`, and `hide`.

- [ ] **Step 1: Add failing banner and glyph contract tests**

```js
test('approved update banner is non-overlapping liquid glass without a leading icon', () => {
  assert.equal(count('id="updateBanner"'), 1);
  assert.equal(count('id="updateBannerVersion"'), 1);
  assert.equal(count('id="updateBannerLater"'), 1);
  assert.equal(count('id="updateBannerInstall"'), 1);
  assert.doesNotMatch(html, /class="update-banner-icon"/);
  assert.match(html, /\.update-banner\{[^}]*grid-template-columns:minmax\(0,1fr\) auto auto/);
  assert.match(html, /\.update-banner\{[^}]*backdrop-filter:blur\(18px\) saturate\(1\.28\)/);
  assert.match(html, /\.update-banner-copy strong\{[^}]*text-overflow:ellipsis[^}]*white-space:nowrap/);
  assert.match(html, /hhQ\('#updateBannerLater'\)\.onclick=/);
  assert.match(html, /hhQ\('#updateBannerInstall'\)\.onclick=/);
});

test('minimize button uses the approved rounded svg line', () => {
  assert.equal(count('class="minimize-glyph"'), 1);
  assert.match(html, /\.minimize-glyph\{[^}]*width:23px/);
  assert.match(html, /\.minimize-glyph path\{[^}]*stroke-width:2\.8/);
  assert.doesNotMatch(html, /id="minimizeButton"[^>]*>−<\/button>/);
});
```

- [ ] **Step 2: Run tests and require the new assertions to fail**

Run: `node --test tests/CompletePrototypeExperienceTests.cjs tests/PrototypeShellContractTests.cjs`

Expected: FAIL because the banner controls and SVG minimize glyph do not exist.

- [ ] **Step 3: Add the approved banner markup and CSS**

```html
<div class="update-banner" id="updateBanner" hidden>
  <div class="update-banner-copy">
    <strong id="updateBannerVersion">发现新版本</strong>
    <small>已完成安全检查，可选择更新</small>
  </div>
  <button class="update-banner-action" id="updateBannerLater">稍后</button>
  <button class="update-banner-action primary" id="updateBannerInstall">更新</button>
</div>
```

Use `grid-template-columns:minmax(0,1fr) auto auto`, `overflow:hidden`, ellipsis text, `rgba(...,.48/.55)`, `backdrop-filter:blur(18px) saturate(1.28)`, and opaque text with a dark text shadow. Insert the banner between the toolbar and filters so it occupies layout space and never covers records.

- [ ] **Step 4: Replace the minimize text glyph**

```html
<button class="icon-button" id="minimizeButton" title="隐藏到后台" aria-label="隐藏到后台">
  <svg class="minimize-glyph" viewBox="0 0 24 24" aria-hidden="true"><path d="M4 12h16"/></svg>
</button>
```

Use a 23px SVG with `stroke:currentColor`, `stroke-width:2.8`, `stroke-linecap:round`, and `fill:none`.

- [ ] **Step 5: Connect the banner to existing update state**

```js
let updateBannerVisible=false, updateCanInstall=false;
function setUpdateBanner(data){
  const available=data.updateAvailable===true;
  updateCanInstall=data.canInstall===true;
  if(!available) updateBannerVisible=false;
  if(data.notifyUser===true) updateBannerVisible=true;
  hhQ('#updateBannerVersion').textContent=`发现新版本 v${String(data.latestVersion||'')}`;
  hhQ('#updateBanner').hidden=!updateBannerVisible;
}
hhQ('#updateBannerLater').onclick=()=>{updateBannerVisible=false;hhQ('#updateBanner').hidden=true;postNative('snoozeUpdate')};
hhQ('#updateBannerInstall').onclick=()=>{openSettings('about');if(updateCanInstall)hhQ('#installUpdateButton').click()};
hhQ('#settingsButton').onclick=()=>openSettings(hhQ('#settingsButton').classList.contains('update-available')?'about':'appearance');
```

Call `setUpdateBanner(data)` from `applyUpdateStatus`. Preserve the red badge while snoozed, hide the banner immediately on “稍后”, and disable banner movement under reduced-motion mode.

- [ ] **Step 6: Run focused Web tests**

Run: `node --test tests/CompletePrototypeExperienceTests.cjs tests/PrototypeShellContractTests.cjs tests/WebShellModuleTests.cjs tests/ProactiveUpdateIntegrationTests.cjs`

Expected: all tests pass.

- [ ] **Step 7: Commit the functional UI checkpoint**

```powershell
git add -- tests/CompletePrototypeExperienceTests.cjs tests/PrototypeShellContractTests.cjs src/HuahaiClipboard.App/Assets/Web/product-shell.html
git commit -m "feat: add main-panel update banner"
```

### Task 2: Version 1.1.8 contract

**Files:**
- Modify: `tests/ReleaseVersionContractTests.ps1`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Modify: `installer/Bootstrapper.cs`
- Modify: `README.md`
- Modify: `.codex/app-product-delivery-interaction-contract.json`
- Modify: `.codex/app-product-delivery-ui-carrier.json`
- Modify: `.codex/app-product-delivery-progress.json`

**Interfaces:**
- Produces application, About page, installer registry metadata, build documentation and delivery manifests consistently reporting `1.1.8`.
- Preserves update-service fixture versions that intentionally exercise older remote releases.

- [ ] **Step 1: Change the release contract to expect 1.1.8**

```powershell
$expected = '1.1.8'
if ($app -notmatch 'CurrentVersion = new\(1, 1, 8\)') { throw 'Application update version is not 1.1.8.' }
```

Update the remaining assertions in the same file for Web shell, installer and README.

- [ ] **Step 2: Run the release contract and require failure**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/ReleaseVersionContractTests.ps1`

Expected: FAIL because source metadata still reports 1.1.7.

- [ ] **Step 3: Bump only release metadata to 1.1.8**

Set `CurrentVersion = new(1, 1, 8)`, Web About/simulation text to `1.1.8`, installer assembly/file/display versions to `1.1.8`, and README build paths/properties to `webview-build-1.1.8` and `/p:Version=1.1.8`. Update delivery manifest revision strings and evidence targets without changing user data paths.

- [ ] **Step 4: Run version and privacy contracts**

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ReleaseVersionContractTests.ps1
node --test tests/GitDataPrivacyTests.cjs
```

Expected: both pass.

- [ ] **Step 5: Commit the version checkpoint**

```powershell
git add -- tests/ReleaseVersionContractTests.ps1 src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs src/HuahaiClipboard.App/Assets/Web/product-shell.html installer/Bootstrapper.cs README.md .codex/app-product-delivery-interaction-contract.json .codex/app-product-delivery-ui-carrier.json .codex/app-product-delivery-progress.json
git commit -m "release: prepare flower clipboard v1.1.8"
```

### Task 3: Verification, installer and public release

**Files:**
- Create ignored artifacts: `dist/webview-build-1.1.8/**`
- Create ignored artifact: `dist/HuahaiClipboard-Setup.exe`
- Create ignored artifact: `dist/HuahaiClipboard-Setup.exe.sha256`
- Modify: release evidence in `.codex/app-product-delivery-progress.json` only if final counts differ from the prepared target.

**Interfaces:**
- Produces signed public GitHub Release `v1.1.8` with fixed installer name and SHA-256 sidecar.
- Does not install, uninstall or overwrite the user's currently installed copy.

- [ ] **Step 1: Run deterministic source suites**

```powershell
dotnet test tests\HuahaiClipboard.Core.Tests\HuahaiClipboard.Core.Tests.csproj -c Release
dotnet test tests\HuahaiClipboard.App.TrayTests\HuahaiClipboard.App.TrayTests.csproj -c Release
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj -c Release
node --test tests\*.cjs tests\HuahaiClipboard.App.Smoke\*Tests.cjs
```

Run all `tests/Installer*Tests.ps1`, `tests/PrerequisiteMetadataTests.ps1`, `tests/ReleaseVersionContractTests.ps1`, and the install-root preservation/swap suites. Expected: zero failures.

- [ ] **Step 2: Build the x64 application without installing it**

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' src\HuahaiClipboard.App\HuahaiClipboard.App.csproj /t:Build /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=false /p:WindowsAppSDKSelfContained=true /p:Version=1.1.8 /p:OutDir="$PWD\dist\webview-build-1.1.8\" /restore /m
```

Require the source and built `Assets/Web/product-shell.html` SHA-256 hashes to match.

- [ ] **Step 3: Build and sign the installer**

```powershell
.\installer\Fetch-Prerequisites.ps1 -Destination dist\prerequisites
.\installer\Build-Installer.ps1 -PublishRoot dist\webview-build-1.1.8 -PrerequisiteRoot dist\prerequisites -OutputPath dist\HuahaiClipboard-Setup.exe -SigningThumbprint CD06B727BD8811C3B59CE0A4F9384D68EC7431C2
```

Verify Authenticode signer `CN=HuahaiClipboard Open Source Release`, installer version `1.1.8`, required payloads and SHA-256.

- [ ] **Step 4: Review and commit final evidence**

Run `git diff --check`, inspect `git status --short`, and verify no `Data/`, clipboard cache, credentials, `.superpowers/`, build output or temporary installer files are tracked. Commit any final delivery-manifest evidence with `docs: record verified v1.1.8 release`.

- [ ] **Step 5: Push and publish**

Push `codex/native-ui-spike`, fast-forward `master` to the same commit, create/push tag `v1.1.8`, and publish a non-draft, non-prerelease GitHub Release containing `HuahaiClipboard-Setup.exe` and `HuahaiClipboard-Setup.exe.sha256`.

- [ ] **Step 6: Verify the public release**

Require local HEAD, remote branch, remote master and tag to resolve to the same release commit. Query the public GitHub Release and require version `1.1.8`, fixed installer name, matching size and SHA-256. Do not launch the installer or alter the local installation.

## Self-Review

- Spec coverage: Task 1 covers banner visibility, no leading icon, layout isolation, increased glass transparency, persistent badge, snooze, direct About routing, reduced motion and the larger minimize glyph. Task 2 covers version consistency and privacy metadata. Task 3 covers full regression, signed packaging, Git publication and old-client discoverability.
- Placeholder scan: no unresolved marker or undefined duplicate step remains.
- Type consistency: all controls use the existing WebView message names `updateStatus`, `snoozeUpdate`, `installUpdate`, and `hide`; version metadata is consistently `1.1.8`.
