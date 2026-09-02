# Data Recovery and Lossless Upgrade Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Recover data affected by the upgrade path defect, then block every future update unless the post-upgrade readable data is a verified superset of the pre-upgrade data.

**Architecture:** Read-only discovery, inspection, snapshot, merge planning, activation, reporting, and health-manifest services live in HuahaiClipboard.Core. A standalone WinUI recovery executable uses those services through Windows registry, filesystem, and DPAPI adapters. The installer resolves stable data identity, creates preflight evidence, reuses the old program path, and keeps the old version backup until it receives a health receipt.

**Tech Stack:** .NET 8, MSTest, WinUI 3, DPAPI CurrentUser, HKCU Registry, SHA-256, C# installer bootstrapper, PowerShell installer probes.

## Global Constraints

- Preserve all existing dirty worktree changes; stage only paths owned by each checkpoint.
- Never test against, move, overwrite, delete, or quarantine the live F:\HuahaiClipboard\Data directory.
- Discovery and inspection are read-only. Before a recovery write, snapshot source and destination and require matching SHA-256 manifests.
- Reports contain paths, provenance, counts, hashes, states, and error codes only. They never contain clipboard bodies, note HTML, or image bytes.
- Existing interactive upgrades reuse the registered install location without opening the folder picker.
- The release gate compares record, todo, and note ID sets plus attachment hashes. Counts alone are not enough.
- No GitHub push, Release upload, manifest update, or public installation is authorized.
- Keep .codex/artifacts/ui-qa/ out of Git.

---

## File Structure

| Path | Responsibility |
| --- | --- |
| src/HuahaiClipboard.Core/Recovery/RecoveryModels.cs | Immutable recovery source, inspection, snapshot, plan, conflict, report, manifest, and receipt contracts. |
| src/HuahaiClipboard.Core/Recovery/RecoverySourceDiscovery.cs | Read-only normalized candidate discovery. |
| src/HuahaiClipboard.Core/Recovery/RecoverySourceInspector.cs | Hash, parse, and DPAPI inspection without source mutation. |
| src/HuahaiClipboard.Core/Recovery/RecoverySnapshotService.cs | Verified snapshot creation and bounded retention. |
| src/HuahaiClipboard.Core/Recovery/RecoveryMergePlanner.cs | Deterministic lossless merge plan. |
| src/HuahaiClipboard.Core/Recovery/RecoveryTransaction.cs | Candidate write, validation, activation, rollback. |
| src/HuahaiClipboard.Core/Services/AtomicJsonFileStore.cs | Shared verified atomic writes and bak1/bak2 rotation. |
| src/HuahaiClipboard.Recovery/ | Standalone recovery application. |
| installer/DataLocationPolicy.cs | Stable HKCU\Software\HuahaiClipboard\DataLocation policy. |
| installer/UpgradePreflightPolicy.cs | Installer-safe snapshot, manifest, and receipt gate. |

## Task 1: Stable Data Identity and Recovery Contracts

**Files:**
- Create: src/HuahaiClipboard.Core/Recovery/RecoveryModels.cs
- Create: src/HuahaiClipboard.Core/Services/DataLocationRegistry.cs
- Modify: src/HuahaiClipboard.Core/Services/LocalDataLayout.cs
- Create: tests/HuahaiClipboard.Core.Tests/DataLocationRegistryTests.cs
- Modify: tests/HuahaiClipboard.Core.Tests/LocalProductPolicyTests.cs

**Produces:** IDataLocationRegistry, DataRootResolution, and RecoverySource. LocalDataLayout.ResolveDataRootAsync uses valid registered data before install and legacy roots and reports RecoveryRequired for multiple populated roots.

- [ ] **Step 1: Write failing priority and ambiguity tests**

~~~csharp
[TestMethod]
public async Task ResolveDataRoot_UsesRegisteredRootBeforeExecutableRoot()
{
    var result = await LocalDataLayout.ResolveDataRootAsync(
        new FakeDataLocationRegistry(@"F:\StableData"), @"F:\Program", @"F:\Legacy", CancellationToken.None);

    Assert.AreEqual(DataRootResolutionKind.Registered, result.Kind);
    Assert.AreEqual(@"F:\StableData", result.DataRoot);
}

[TestMethod]
public async Task ResolveDataRoot_ReportsRecoveryRequiredForTwoPopulatedRoots()
{
    using var fixture = DataFixture.CreateTwoPopulatedRoots();
    var result = await LocalDataLayout.ResolveDataRootAsync(
        new FakeDataLocationRegistry(null), fixture.ProgramRoot, fixture.LegacyRoot, CancellationToken.None);

    Assert.AreEqual(DataRootResolutionKind.RecoveryRequired, result.Kind);
}
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter FullyQualifiedName~DataLocationRegistryTests

Expected: compilation failure because the registry contract and resolver do not exist.

- [ ] **Step 3: Implement the smallest deterministic resolver**

~~~csharp
public enum DataRootResolutionKind { Registered, InstallRoot, Legacy, NewInstall, RecoveryRequired }
public sealed record DataRootResolution(DataRootResolutionKind Kind, string? DataRoot, IReadOnlyList<string> Candidates);
public interface IDataLocationRegistry
{
    Task<string?> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(string dataRoot, CancellationToken cancellationToken);
}
~~~

Normalize paths; reject invalid roots; define populated as a direct SID child containing a recognized data file. More than one populated root returns RecoveryRequired, never a timestamp-based guess.

- [ ] **Step 4: Verify GREEN**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~DataLocationRegistryTests|FullyQualifiedName~LocalProductPolicyTests"

Expected: all selected tests pass.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- src/HuahaiClipboard.Core/Recovery/RecoveryModels.cs src/HuahaiClipboard.Core/Services/DataLocationRegistry.cs src/HuahaiClipboard.Core/Services/LocalDataLayout.cs tests/HuahaiClipboard.Core.Tests/DataLocationRegistryTests.cs tests/HuahaiClipboard.Core.Tests/LocalProductPolicyTests.cs
git commit -m "feat: resolve stable clipboard data locations"
~~~

## Task 2: Read-Only Discovery and Inspection

**Files:**
- Create: src/HuahaiClipboard.Core/Recovery/RecoverySourceDiscovery.cs
- Create: src/HuahaiClipboard.Core/Recovery/RecoverySourceInspector.cs
- Create: tests/HuahaiClipboard.Core.Tests/RecoverySourceDiscoveryTests.cs
- Create: tests/HuahaiClipboard.Core.Tests/RecoverySourceInspectorTests.cs

**Produces:** RecoverySourceDiscovery.Discover and RecoverySourceInspector.InspectAsync, both source-read-only.

- [ ] **Step 1: Write failing inspection regression test**

~~~csharp
[TestMethod]
public async Task InspectAsync_ReportsMalformedHistoryWithoutRenamingIt()
{
    using var fixture = RecoveryFixture.Create();
    var path = fixture.Write("history.dat", "not-a-dpapi-payload");

    var result = await new RecoverySourceInspector(new FixtureRecoveryReader())
        .InspectAsync(fixture.Source(), CancellationToken.None);

    Assert.AreEqual(RecoveryInspectionState.Malformed, result.State);
    Assert.IsTrue(File.Exists(path));
    Assert.IsFalse(File.Exists(path + ".corrupt"));
}
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~RecoverySourceDiscoveryTests|FullyQualifiedName~RecoverySourceInspectorTests"

Expected: missing services and contracts.

- [ ] **Step 3: Implement bounded discovery and inspection**

Discover explicit, registered, uninstall, shortcut, installer-log, install-root, legacy, backup-sibling, and history.dat.corrupt parents. Skip reparse points and inaccessible paths. Full-drive traversal is permitted only with a caller-provided deep-scan root.

Inspection creates a sorted relative-path SHA-256 manifest, uses injected production readers, and returns only Readable, EncryptedForAnotherUser, Malformed, Incomplete, Duplicate, or Unavailable.

- [ ] **Step 4: Verify GREEN**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~RecoverySourceDiscoveryTests|FullyQualifiedName~RecoverySourceInspectorTests"

Expected: all selected tests pass without any source file mutation.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- src/HuahaiClipboard.Core/Recovery/RecoverySourceDiscovery.cs src/HuahaiClipboard.Core/Recovery/RecoverySourceInspector.cs tests/HuahaiClipboard.Core.Tests/RecoverySourceDiscoveryTests.cs tests/HuahaiClipboard.Core.Tests/RecoverySourceInspectorTests.cs
git commit -m "feat: inspect clipboard recovery sources safely"
~~~

## Task 3: Snapshots, Manifests, and Lossless Merge

**Files:**
- Create: src/HuahaiClipboard.Core/Recovery/RecoverySnapshotService.cs
- Create: src/HuahaiClipboard.Core/Recovery/UpgradeHealthContract.cs
- Create: src/HuahaiClipboard.Core/Recovery/RecoveryMergePlanner.cs
- Create: tests/HuahaiClipboard.Core.Tests/RecoverySnapshotServiceTests.cs
- Create: tests/HuahaiClipboard.Core.Tests/UpgradeHealthContractTests.cs
- Create: tests/HuahaiClipboard.Core.Tests/RecoveryMergePlannerTests.cs

**Produces:** verified snapshots, UpgradeDataManifest.IsSupersetOf, and immutable RecoveryPlan.

- [ ] **Step 1: Write failing snapshot, manifest, and merge tests**

~~~csharp
[TestMethod]
public async Task CreateAsync_CopiesAnEquivalentHashManifest()
{
    using var fixture = RecoveryFixture.Create();
    fixture.Write("S-1-5-21-1000/history.dat", "fixture");

    var snapshot = await new RecoverySnapshotService()
        .CreateAsync(fixture.Source(), fixture.SnapshotRequest(), CancellationToken.None);

    CollectionAssert.AreEquivalent(snapshot.SourceManifest.Keys.ToArray(), snapshot.CopyManifest.Keys.ToArray());
}

[TestMethod]
public void IsSupersetOf_IsFalseWhenAnOldAttachmentHashIsMissing()
{
    Assert.IsFalse(after.IsSupersetOf(before));
}

[TestMethod]
public void CreatePlan_KeepsBothPayloadsWhenOneHistoryIdConflicts()
{
    var plan = new RecoveryMergePlanner().CreatePlan(source, destination);
    Assert.AreEqual(2, plan.History.Count);
}
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~RecoverySnapshotServiceTests|FullyQualifiedName~UpgradeHealthContractTests|FullyQualifiedName~RecoveryMergePlannerTests"

Expected: missing snapshot, manifest, and merge types.

- [ ] **Step 3: Implement immutable planning**

Snapshot only under a supplied normalized parent, refuse overlapping source/destination paths, reject reparse points, compare every file hash, and retain at most three verified snapshots per SID for 30 days.

Manifest contains stable record/todo/note IDs and attachment hashes, never data bodies. The planner matches history by ID then semantic key, unions pinned/favorite states, retains newest timestamps, clones conflicting history payloads with a new ID, appends unique todos with normalized order, and clones conflicting notes with a recovery-copy suffix.

- [ ] **Step 4: Verify GREEN**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~RecoverySnapshotServiceTests|FullyQualifiedName~UpgradeHealthContractTests|FullyQualifiedName~RecoveryMergePlannerTests"

Expected: all selected tests pass, including overlap refusal and no count-only comparison.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- src/HuahaiClipboard.Core/Recovery/RecoverySnapshotService.cs src/HuahaiClipboard.Core/Recovery/UpgradeHealthContract.cs src/HuahaiClipboard.Core/Recovery/RecoveryMergePlanner.cs tests/HuahaiClipboard.Core.Tests/RecoverySnapshotServiceTests.cs tests/HuahaiClipboard.Core.Tests/UpgradeHealthContractTests.cs tests/HuahaiClipboard.Core.Tests/RecoveryMergePlannerTests.cs
git commit -m "feat: plan lossless clipboard recovery"
~~~

## Task 4: Atomic Recovery Activation and Durable Stores

**Files:**
- Create: src/HuahaiClipboard.Core/Recovery/RecoveryTransaction.cs
- Create: src/HuahaiClipboard.Core/Recovery/RecoveryReportWriter.cs
- Create: src/HuahaiClipboard.Core/Services/AtomicJsonFileStore.cs
- Modify: src/HuahaiClipboard.Core/Services/JsonClipboardHistorySource.cs
- Modify: src/HuahaiClipboard.Core/Services/JsonSettingsStore.cs
- Modify: src/HuahaiClipboard.Core/Services/JsonWindowPlacementStore.cs
- Modify: src/HuahaiClipboard.Core/Todo/JsonTodoWorkspaceStore.cs
- Create: tests/HuahaiClipboard.Core.Tests/RecoveryTransactionTests.cs
- Create: tests/HuahaiClipboard.Core.Tests/AtomicJsonFileStoreTests.cs
- Modify: tests/HuahaiClipboard.Core.Tests/ProductionClipboardTests.cs
- Modify: tests/HuahaiClipboard.Core.Tests/JsonSettingsStoreTests.cs
- Modify: tests/HuahaiClipboard.Core.Tests/WindowPlacementStoreTests.cs
- Modify: tests/HuahaiClipboard.Core.Tests/TodoWorkspaceStoreTests.cs

**Produces:** RecoveryTransaction.ApplyAsync, metadata-only reports, and verified file writes with bak1/bak2.

- [ ] **Step 1: Write failing rollback and backup regression tests**

~~~csharp
[TestMethod]
public async Task ApplyAsync_RestoresDestinationWhenCandidateLosesAnOldId()
{
    var result = await transaction.ApplyAsync(plan, request, CancellationToken.None);

    Assert.AreEqual(RecoveryTransactionState.RolledBack, result.State);
    CollectionAssert.Contains(await fixture.ReadDestinationIdsAsync(), fixture.ExistingHistoryId);
}

[TestMethod]
public async Task WriteVerifiedAsync_RetainsTwoPreviousVerifiedCopies()
{
    await store.WriteVerifiedAsync(path, new[] { "one" }, Deserialize, Validate, CancellationToken.None);
    await store.WriteVerifiedAsync(path, new[] { "two" }, Deserialize, Validate, CancellationToken.None);
    await store.WriteVerifiedAsync(path, new[] { "three" }, Deserialize, Validate, CancellationToken.None);

    CollectionAssert.AreEqual(new[] { "two" }, await ReadAsync(path + ".bak1"));
    CollectionAssert.AreEqual(new[] { "one" }, await ReadAsync(path + ".bak2"));
}
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~RecoveryTransactionTests|FullyQualifiedName~AtomicJsonFileStoreTests|FullyQualifiedName~ProductionClipboardTests"

Expected: missing transaction/atomic writer or the existing history quarantine behavior fails preservation assertion.

- [ ] **Step 3: Implement fail-closed activation and writes**

Write recovery output to a sibling candidate data directory. Copy images to destination-owned stores and rewrite paths. Reopen candidate with production readers, compare against baseline manifest, then atomically activate; on failure restore destination and retain snapshots.

For JSON stores: write unique temporary file, flush, parse/validate temporary through the production reader, atomically replace active file, then rotate two verified backups. History parse/DPAPI failures return typed recovery-required state; never rename the only copy and return an empty history.

- [ ] **Step 4: Verify GREEN**

Run: dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "FullyQualifiedName~RecoveryTransactionTests|FullyQualifiedName~AtomicJsonFileStoreTests|FullyQualifiedName~ProductionClipboardTests|FullyQualifiedName~JsonSettingsStoreTests|FullyQualifiedName~WindowPlacementStoreTests|FullyQualifiedName~TodoWorkspaceStoreTests"

Expected: all selected tests pass and all stores retain bak1 and bak2 after three writes.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- src/HuahaiClipboard.Core/Recovery/RecoveryTransaction.cs src/HuahaiClipboard.Core/Recovery/RecoveryReportWriter.cs src/HuahaiClipboard.Core/Services/AtomicJsonFileStore.cs src/HuahaiClipboard.Core/Services/JsonClipboardHistorySource.cs src/HuahaiClipboard.Core/Services/JsonSettingsStore.cs src/HuahaiClipboard.Core/Services/JsonWindowPlacementStore.cs src/HuahaiClipboard.Core/Todo/JsonTodoWorkspaceStore.cs tests/HuahaiClipboard.Core.Tests
git commit -m "feat: preserve local clipboard data on failures"
~~~

## Task 5: Standalone Recovery Application

**Files:**
- Create: src/HuahaiClipboard.Recovery/HuahaiClipboard.Recovery.csproj
- Create: src/HuahaiClipboard.Recovery/Program.cs
- Create: src/HuahaiClipboard.Recovery/RecoveryCompositionRoot.cs
- Create: src/HuahaiClipboard.Recovery/Presentation/RecoveryWindow.xaml
- Create: src/HuahaiClipboard.Recovery/Presentation/RecoveryWindow.xaml.cs
- Create: src/HuahaiClipboard.Recovery/Presentation/RecoveryViewModel.cs
- Modify: HuahaiClipboard.sln
- Create: tests/HuahaiClipboard.Recovery.Tests/HuahaiClipboard.Recovery.Tests.csproj
- Create: tests/HuahaiClipboard.Recovery.Tests/RecoveryViewModelTests.cs

**Produces:** ScanAsync, InspectAsync, CreatePlanAsync, and ApplyAsync operations connected only to shared recovery services.

- [ ] **Step 1: Write failing workflow test**

~~~csharp
[TestMethod]
public async Task ScanThenRecover_RequiresReadableInspectionAndSnapshotsBeforeApply()
{
    var viewModel = new RecoveryViewModel(fakeWorkflow);

    await viewModel.ScanAsync(CancellationToken.None);
    Assert.IsFalse(viewModel.CanApply);

    await viewModel.InspectAsync(viewModel.Sources.Single(), CancellationToken.None);
    await viewModel.CreatePlanAsync(CancellationToken.None);
    Assert.IsTrue(viewModel.CanApply);
}
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/HuahaiClipboard.Recovery.Tests/HuahaiClipboard.Recovery.Tests.csproj --filter FullyQualifiedName~RecoveryViewModelTests

Expected: recovery application project/types do not exist.

- [ ] **Step 3: Implement a focused native workflow**

The window has a status banner, source list, local-only disclosure, Scan, cancellable Deep Scan, Create Plan, and Merge Recovery. Apply remains disabled until source/destination snapshots and plan verification succeed. Rows and reports show only path, provenance, counts, state, conflicts, and report location.

- [ ] **Step 4: Verify GREEN and build**

Run: dotnet test tests/HuahaiClipboard.Recovery.Tests/HuahaiClipboard.Recovery.Tests.csproj --filter FullyQualifiedName~RecoveryViewModelTests

Run: dotnet build src/HuahaiClipboard.Recovery/HuahaiClipboard.Recovery.csproj -c Release -p:Platform=x64

Expected: test passes; x64 build has zero warnings and errors.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- HuahaiClipboard.sln src/HuahaiClipboard.Recovery tests/HuahaiClipboard.Recovery.Tests
git commit -m "feat: add standalone clipboard recovery tool"
~~~

## Task 6: Installer Preflight, Target Reuse, and Receipt Gate

**Files:**
- Create: installer/DataLocationPolicy.cs
- Create: installer/UpgradePreflightPolicy.cs
- Modify: installer/Bootstrapper.cs
- Modify: installer/InstallSwapTransaction.cs
- Modify: installer/Build-Installer.ps1
- Modify: tests/InstallerUiContractTests.ps1
- Modify: tests/InstallerLocationPolicyTests.ps1
- Modify: tests/InstallerDataPreservationTests.ps1
- Modify: tests/InstallerSwapTransactionTests.ps1
- Create: tests/InstallerUpgradePreflightTests.ps1

**Produces:** installer-safe stable data resolution, verified preflight snapshot/manifest, no-directory-switch rule, and deferred old-backup cleanup.

- [ ] **Step 1: Write failing policy probes**

~~~csharp
Assert(BootstrapperPolicy.ResolveInteractiveInstallRoot(
    @"F:\Existing\HuahaiClipboard", null, false) == @"F:\Existing\HuahaiClipboard",
    "existing interactive upgrade must reuse registered path");

Throws(() => BootstrapperPolicy.ResolveInteractiveInstallRoot(
    @"F:\Existing\HuahaiClipboard", @"G:\Other\HuahaiClipboard", false),
    "normal upgrade must reject changing program location");
~~~

Add probes proving preflight fails without verified snapshot and old backup remains until a valid health receipt proves a superset.

- [ ] **Step 2: Verify RED**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerLocationPolicyTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerUpgradePreflightTests.ps1

Expected: missing policy API or legacy folder-picker behavior fails assertions.

- [ ] **Step 3: Implement fail-closed installer flow**

Read registered InstallLocation before default selection. Existing interactive upgrade uses it without ChooseInstallRoot. Different --install-dir is rejected unless a future explicit migration mode is supplied.

Before swap, resolve/write stable DataLocation, open data with production readers, create snapshot and baseline manifest, and write a one-time candidate token. Keep old backup until candidate's token-bound receipt proves the post-start manifest is a superset.

- [ ] **Step 4: Verify GREEN**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerUiContractTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerLocationPolicyTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerDataPreservationTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerSwapTransactionTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerUpgradePreflightTests.ps1

Expected: every installer policy test passes, proving no directory switch and no early backup deletion.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- installer/DataLocationPolicy.cs installer/UpgradePreflightPolicy.cs installer/Bootstrapper.cs installer/InstallSwapTransaction.cs installer/Build-Installer.ps1 tests/InstallerUiContractTests.ps1 tests/InstallerLocationPolicyTests.ps1 tests/InstallerDataPreservationTests.ps1 tests/InstallerSwapTransactionTests.ps1 tests/InstallerUpgradePreflightTests.ps1
git commit -m "feat: block unsafe clipboard upgrades"
~~~

## Task 7: First-Start Health Gate and Recovery Entry

**Files:**
- Modify: src/HuahaiClipboard.App/CompositionRoot.cs
- Modify: src/HuahaiClipboard.App/Program.cs
- Modify: src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs
- Modify: src/HuahaiClipboard.App/Assets/Web/product-shell.html
- Modify: src/HuahaiClipboard.Core/Services/WebBridgeProtocol.cs
- Create: tests/HuahaiClipboard.App.IntegrationTests/UpgradeHealthStartupTests.cs
- Modify: tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs
- Modify: tests/PrototypeShellContractTests.cjs

**Produces:** a startup state that blocks capture/retention/empty-data initialization before receipt verification and a recovery command only for recovery-required state.

- [ ] **Step 1: Write failing startup-order and shell tests**

~~~csharp
[TestMethod]
public async Task Initialize_DoesNotStartCaptureBeforeHealthReceiptExists()
{
    var result = await fixture.InitializeAsync(receipt: null);

    Assert.IsFalse(result.CaptureStarted);
    Assert.AreEqual(StartupDataState.RecoveryRequired, result.DataState);
}
~~~

~~~js
test("recovery-required shell exposes a working recovery command", () => {
  assert.match(shell, /data-action="open-recovery"/);
});
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj --filter FullyQualifiedName~UpgradeHealthStartupTests

Run: node --test tests/PrototypeShellContractTests.cjs

Expected: missing health state integration and shell action.

- [ ] **Step 3: Implement first-start safety**

Resolve data root and verify candidate health before settings load, retention, history projection, capture registration, or shell writes. Success writes token-bound receipt. Failure blocks capture and routes to standalone recovery. The healthy shell gains no persistent new control.

- [ ] **Step 4: Verify GREEN**

Run: dotnet test tests/HuahaiClipboard.App.IntegrationTests/HuahaiClipboard.App.IntegrationTests.csproj --filter FullyQualifiedName~UpgradeHealthStartupTests

Run: node --test tests/PrototypeShellContractTests.cjs

Expected: all selected tests pass; normal shell has no recovery noise.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- src/HuahaiClipboard.App/CompositionRoot.cs src/HuahaiClipboard.App/Program.cs src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs src/HuahaiClipboard.App/Assets/Web/product-shell.html src/HuahaiClipboard.Core/Services/WebBridgeProtocol.cs tests/HuahaiClipboard.App.IntegrationTests/UpgradeHealthStartupTests.cs tests/HuahaiClipboard.Core.Tests/ShellIntegrationPolicyTests.cs tests/PrototypeShellContractTests.cjs
git commit -m "feat: route unsafe startup to recovery"
~~~

## Task 8: Isolated Release Matrix

**Files:**
- Create: tests/update-evidence/LosslessUpgradeMatrix.ps1
- Create: tests/update-evidence/RecoveryFixture.ps1
- Modify: tests/update-evidence/InstallerFaultInjection.ps1
- Modify: tests/update-evidence/HostedWindowsInstalledUpgrade.ps1
- Modify: docs/product/evidence/README.md

**Produces:** isolated machine-readable evidence for upgrades from v1.1.11, v1.1.12, and v1.1.13.

- [ ] **Step 1: Write a failing different-directory scenario**

~~~powershell
$result = Invoke-LosslessUpgradeScenario -Scenario "registered-install-new-requested-directory" -OldVersion "v1.1.13" -RequestedInstallRoot $otherRoot
if ($result.Status -ne "blocked-before-swap") {
  throw "Different directory upgrade must be blocked before program or data swap."
}
~~~

- [ ] **Step 2: Verify RED**

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/update-evidence/LosslessUpgradeMatrix.ps1 -Scenario registered-install-new-requested-directory -WorkRoot <validated-temp-root> -EvidenceRoot <validated-temp-evidence-root>

Expected: missing runner or old installer behavior fails assertion.

- [ ] **Step 3: Implement isolated fixtures and fault injection**

Test text, link, protected image, external file, favorite, pinned, ordered todos, note image, settings, and window positions. Cover same path, missing registry, different requested path, corrupt history, wrong-user DPAPI, lock, low space, snapshot failure, swap failure, missing receipt, and interruption. Every cleanup target must be a verified descendant of supplied temporary work root.

- [ ] **Step 4: Run verification matrix**

Run: dotnet test HuahaiClipboard.sln -c Release --no-restore

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerUiContractTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerLocationPolicyTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerDataPreservationTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerSwapTransactionTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerUpgradePreflightTests.ps1

Run: powershell -NoProfile -ExecutionPolicy Bypass -File tests/update-evidence/LosslessUpgradeMatrix.ps1 -All -WorkRoot <validated-temp-root> -EvidenceRoot <validated-temp-evidence-root>

Run: git diff --check

Expected: every scenario reports verified superset or verified rollback; no live data path is used; no whitespace errors.

- [ ] **Step 5: Commit only owned files**

~~~powershell
git add -- tests/update-evidence/LosslessUpgradeMatrix.ps1 tests/update-evidence/RecoveryFixture.ps1 tests/update-evidence/InstallerFaultInjection.ps1 tests/update-evidence/HostedWindowsInstalledUpgrade.ps1 docs/product/evidence/README.md
git commit -m "test: verify lossless clipboard upgrades"
~~~

## Plan Self-Review

- Tasks 1-4 cover stable location, discovery, inspection, snapshotting, merge planning, activation, report, and durable data writes.
- Task 5 gives affected users an independent real recovery workflow.
- Tasks 6-7 enforce installer preflight, original target reuse, receipt-based rollback, and recovery-required startup.
- Task 8 verifies released-version paths and failure injection without real user data.
- Every task has exact files, concrete API or behavior, a failure-first test, a pass command, and an owned commit set.
