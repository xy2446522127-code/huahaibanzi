# Proactive Update Notifications Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a running Huahai Clipboard discover a newer GitHub Release in the background and proactively remind the user without requiring them to open Settings or click Check for updates.

**Architecture:** Keep `GitHubUpdateCheckService` as the Release metadata authority, add a single cancellable coordinator for immediate and five-minute checks, and project one shared update state into the existing tray and approved WebView shell. Persist only the 24-hour snooze version/deadline in the existing install-root per-user settings; clipboard data never leaves the device.

**Tech Stack:** .NET 8, WinUI 3, Windows Forms `NotifyIcon`, WebView2, HTML/CSS/JavaScript, xUnit, Node contract tests, PowerShell installer/release scripts.

## Global Constraints

- Windows 10/11 x64; no new runtime service, account, administrator requirement, or heavyweight dependency.
- Check immediately after UI readiness, then every 5 minutes while enabled.
- First check failure retries after 15 minutes; repeated failures retry after 60 minutes; success restores the 5-minute interval.
- A newer version produces a tray balloon, persistent main-panel red dot, next-summon prompt, and full About-page state.
- The same version produces at most one proactive popup per process run; “Remind later” suppresses popups for 24 hours while leaving the red dot visible.
- GitHub `ETag`/conditional requests must reuse the last successful result on HTTP 304.
- A Git commit alone is not an update: the maintainer must publish a higher-version GitHub Release with `HuahaiClipboard-Setup.exe`.
- Preserve the approved `product-shell.html` visual system; only the update indicator, status, and update actions may change.
- Do not install or overwrite the app currently installed at `F:\HuahaiClipboard`; the user will manually verify updating.

---

### Task 1: Scheduling and reminder policy

**Files:**
- Create: `src/HuahaiClipboard.Core/Services/UpdateReminderPolicy.cs`
- Create: `src/HuahaiClipboard.Core/Services/ProactiveUpdateCoordinator.cs`
- Create: `tests/HuahaiClipboard.Core.Tests/UpdateReminderPolicyTests.cs`
- Create: `tests/HuahaiClipboard.Core.Tests/ProactiveUpdateCoordinatorTests.cs`

**Interfaces:**
- Produces: `UpdateReminderPolicy.ShouldNotify(Version latestVersion, string? snoozedVersion, DateTimeOffset? snoozedUntil, DateTimeOffset now) : bool`.
- Produces: `UpdateReminderPolicy.DelayAfterFailure(int consecutiveFailures) : TimeSpan` and constants for 5-minute polling, 15/60-minute backoff, and 24-hour snooze.
- Produces: `ProactiveUpdateCoordinator.Start()` and `ValueTask DisposeAsync()` around injected check, enablement, result, error, and delay delegates.

- [ ] **Step 1: Write failing policy tests** covering active snooze, expired snooze, different-version snooze, and exact delay values.
- [ ] **Step 2: Run** `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~UpdateReminderPolicyTests"` and expect missing-type compilation failure.
- [ ] **Step 3: Implement the minimal pure policy** with the exact constants and version-aware snooze behavior.
- [ ] **Step 4: Run the focused policy tests** and expect all to pass.
- [ ] **Step 5: Write failing coordinator tests** proving immediate first check, no overlapping check, five-minute success delay, 15/60-minute failure backoff, reset after success, disabled state, and cancellation.
- [ ] **Step 6: Run** `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~ProactiveUpdateCoordinatorTests"` and expect missing-type compilation failure.
- [ ] **Step 7: Implement the coordinator** as one background loop guarded against duplicate `Start()` calls and canceled by `DisposeAsync()`.
- [ ] **Step 8: Run both focused test classes** and expect all to pass.
- [ ] **Step 9: Checkpoint** with message `feat: add proactive update scheduling policy`.

### Task 2: Conditional GitHub Release requests

**Files:**
- Modify: `src/HuahaiClipboard.Core/Services/GitHubUpdateCheckService.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/GitHubUpdateCheckServiceTests.cs`

**Interfaces:**
- Consumes: existing `CheckAsync(CancellationToken) : Task<UpdateCheckResult>`.
- Produces: the same public API, now sending `If-None-Match` after a successful response and returning the cached successful result on HTTP 304.

- [ ] **Step 1: Add a failing test** whose handler returns `ETag: "release-v1"`, then 304, and assert the second request includes `If-None-Match` and yields the first parsed result.
- [ ] **Step 2: Run** `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~GitHubUpdateCheckServiceTests"` and expect the conditional-request assertion to fail.
- [ ] **Step 3: Replace the API `GetAsync` call with `HttpRequestMessage`**, cache the last successful ETag/result in memory, and handle 304 without changing fallback behavior for 403/429.
- [ ] **Step 4: Run the focused service tests** and expect all to pass.
- [ ] **Step 5: Checkpoint** with message `feat: cache GitHub release checks with etags`.

### Task 3: Tray notification channel

**Files:**
- Modify: `src/HuahaiClipboard.App/Infrastructure/Tray/TrayService.cs`
- Modify: `tests/HuahaiClipboard.App.TrayTests/TrayServiceTests.cs`

**Interfaces:**
- Produces: constructor callback `Action showUpdate`.
- Produces: `SetUpdateAvailable(Version? latestVersion)` and `NotifyUpdateAvailable(Version latestVersion)`.
- Behavior: a hidden-by-default update menu item becomes visible for newer versions; balloon/menu clicks open the existing update page.

- [ ] **Step 1: Add failing tray tests** for the hidden initial item, visible `发现 vX.Y.Z` item, balloon metadata, click routing, and clearing state.
- [ ] **Step 2: Run** `dotnet test tests/HuahaiClipboard.App.TrayTests/HuahaiClipboard.App.TrayTests.csproj` and expect missing-constructor/method failures.
- [ ] **Step 3: Implement the update menu item and balloon** using the existing `NotifyIcon`, without a second process or notification dependency.
- [ ] **Step 4: Run tray tests** and expect all to pass.
- [ ] **Step 5: Checkpoint** with message `feat: notify users of updates from the tray`.

### Task 4: Persistent snooze and desktop integration

**Files:**
- Modify: `src/HuahaiClipboard.Core/Settings/BehaviorSettings.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/WebBridgeProtocol.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/WebBridgeRequest.cs`
- Modify: relevant tests under `tests/HuahaiClipboard.Core.Tests/`.

**Interfaces:**
- Adds optional settings `SnoozedUpdateVersion : string?` and `UpdateSnoozeUntil : DateTimeOffset?`.
- Adds bridge action `snoozeUpdate`.
- Produces one shared `HandleUpdateResultAsync(UpdateCheckResult result, bool allowNotification)` path used by startup, periodic, and manual checks.

- [ ] **Step 1: Add failing serialization/bridge/integration policy tests** for snooze persistence, `snoozeUpdate`, coordinator creation/disposal, and `notifyUser` update-status fields.
- [ ] **Step 2: Run the focused tests** and expect failures because the fields/action/coordinator integration do not exist.
- [ ] **Step 3: Add backward-compatible optional settings and bridge action** so existing settings JSON loads unchanged.
- [ ] **Step 4: Integrate one coordinator into the window lifecycle**, marshal UI work through `DispatcherQueue`, update tray state for every result, and dispose the loop on exit.
- [ ] **Step 5: Implement 24-hour snooze saving** through the existing settings store, retaining the visible badge while suppressing proactive popups.
- [ ] **Step 6: Run focused and full Core/tray tests** and expect zero failures.
- [ ] **Step 7: Checkpoint** with message `feat: integrate persistent proactive update reminders`.

### Task 5: Approved WebView update UI derivative

**Files:**
- Modify: `src/HuahaiClipboard.App/Assets/Web/product-shell.html`
- Modify: `tests/PrototypeShellContractTests.cjs`
- Modify: `tests/CompletePrototypeExperienceTests.cjs`

**Interfaces:**
- Consumes `updateStatus` fields `updateAvailable`, `latestVersion`, `notifyUser`, and `snoozedUntil`.
- Produces `snoozeUpdate` action.
- UI: a small red dot on `#settingsButton`, a one-time summon toast, and an About-page “稍后提醒” button; no other approved layout or material changes.

- [ ] **Step 1: Add failing Node contract tests** asserting the badge, snooze control/action, `notifyUser` handling, and persistent badge when snoozed.
- [ ] **Step 2: Run** `node --test tests/PrototypeShellContractTests.cjs tests/CompletePrototypeExperienceTests.cjs` and expect the new assertions to fail.
- [ ] **Step 3: Add the minimal HTML/CSS/JS derivative**, reusing existing tokens and toast behavior and preventing duplicate prompts for the same version in one Web session.
- [ ] **Step 4: Run both Node suites** and expect all to pass with no console-contract failures.
- [ ] **Step 5: Run the interaction inventory and delivery UI gate**; expect no unexplained or dead controls.
- [ ] **Step 6: Checkpoint** with message `feat: surface update reminders in the main panel`.

### Task 6: v1.1.7 release and old-version discovery proof

**Files:**
- Modify: all existing version contract locations resolved by `tests/ReleaseVersionContractTests.ps1`.
- Modify: `.codex/app-product-delivery-progress.json`.
- Create: build artifacts only under ignored `dist/`.

**Interfaces:**
- Produces GitHub Release `v1.1.7` with fixed asset `HuahaiClipboard-Setup.exe` and SHA-256 sidecar.
- Preserves installed `F:\HuahaiClipboard` version and timestamp.

- [ ] **Step 1: Change release-contract tests to expect `1.1.7`**, run them, and expect failure against the current `1.1.6` source.
- [ ] **Step 2: Bump source, Web shell, installer, README, and assembly versions to `1.1.7`** without touching the installed application.
- [ ] **Step 3: Run Core, tray, native UI, Web, privacy, installer-swap, install-root preservation, and release-version suites** and require zero failures.
- [ ] **Step 4: Run the app-product-delivery release gate**, inspect the final diff, and scan tracked/staged files for secrets, user data, installer temp data, and UI carrier drift.
- [ ] **Step 5: Build x64 output under `dist/webview-build-1.1.7`**, verify source/built `product-shell.html` hashes match, and verify executable startup dependencies without installing it.
- [ ] **Step 6: Build and sign `dist/HuahaiClipboard-Setup.exe`**, verify signer `CN=HuahaiClipboard Open Source Release`, installer contents, version, and SHA-256.
- [ ] **Step 7: Create the verified release checkpoint** with message `release: add proactive update notifications in v1.1.7`.
- [ ] **Step 8: Push the current branch and `master`, create/push tag `v1.1.7`, and publish a non-draft non-prerelease GitHub Release** with the installer and SHA-256 asset.
- [ ] **Step 9: Verify local HEAD, remote branches, tag, and Release resolve to the same commit and assets**.
- [ ] **Step 10: Probe the still-installed v1.1.5 update service against GitHub** and require `updateAvailable=true`, `latest=1.1.7`, and `canAutoInstall=true`; recheck that the installed executable version/timestamp are unchanged.
- [ ] **Step 11: Report exact installer path, Release URL, test counts, limitations, and manual verification steps**; do not launch the installer.

## Self-Review

- Spec coverage: startup/periodic checks, ETag, backoff, one-per-run reminder, 24-hour snooze, tray fallback, panel badge, About actions, privacy, offline behavior, versioning, release publication, and untouched local installation all map to Tasks 1–6.
- Placeholder scan: no `TODO`, `TBD`, “implement later”, or undefined “similar to” steps remain.
- Type consistency: the policy, coordinator, settings, tray, bridge, and Web message names are identical at each producer/consumer boundary.
