# Static Update Manifest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let new Huahai Clipboard clients discover a signed release without relying on the GitHub Releases API quota.

**Architecture:** `GitHubUpdateCheckService` accepts an optional static-manifest fetcher. It returns a validated `UpdateCheckResult` from the fixed raw GitHub manifest before executing its existing API/HTML fallback. The existing installer hash and publisher verification remain the trust enforcement points.

**Tech Stack:** .NET 8, `HttpClient`, `System.Text.Json`, MSTest, GitHub raw content.

## Global Constraints

- Do not embed a GitHub token or add a third-party service.
- Keep installer URL validation pinned to HTTPS `github.com` and preserve publisher-thumbprint verification.
- A failed static manifest must fall back to the existing GitHub API behavior.
- Only 1.1.13 and later clients gain this route; 1.1.11 and 1.1.12 remain unchanged binaries.

---

### Task 1: Validate and Consume the Static Manifest

**Files:**
- Create: `src/HuahaiClipboard.Core/Services/StaticUpdateManifest.cs`
- Modify: `src/HuahaiClipboard.Core/Services/GitHubUpdateCheckService.cs`
- Modify: `tests/HuahaiClipboard.Core.Tests/GitHubUpdateCheckServiceTests.cs`

**Interfaces:**
- Produces `StaticUpdateManifest.TryCreateUpdate` and a manifest-first `CheckAsync` path.

- [ ] **Step 1: Write failing behavior tests**

```csharp
[TestMethod]
public async Task CheckAsync_UsesValidatedStaticManifestWithoutCallingReleaseApi()
{
    var manifest = """{\"version\":\"1.1.13\",\"installerUrl\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.13/HuahaiClipboard-Setup.exe\",\"size\":42,\"sha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}""";
    var result = await service.CheckAsync(CancellationToken.None);
    Assert.IsTrue(result.CanAutoInstall);
    Assert.AreEqual(0, releaseApiRequests);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj -c Release --filter "FullyQualifiedName~GitHubUpdateCheckServiceTests"`

Expected: the static-manifest constructor path does not exist.

- [ ] **Step 3: Implement strict parsing and fallback**

```csharp
public sealed record StaticUpdateManifest(string Version, string InstallerUrl, long Size, string Sha256, string ReleaseUrl);
```

Fetch `https://raw.githubusercontent.com/xy2446522127-code/huahaibanzi/master/update-manifest.json` with a 12-second timeout. Return a result only for a complete, newer, valid manifest. Catch manifest transport and validation errors and continue to the existing API request.

- [ ] **Step 4: Cover malformed and non-newer manifests**

Assert that a manifest with `size: 0`, an invalid hash, or `version: 1.1.12` does not suppress the existing API result.

- [ ] **Step 5: Run the focused suite and commit**

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj -c Release --filter "FullyQualifiedName~GitHubUpdateCheckServiceTests"`

Commit message: `feat: add static update manifest fallback`

### Task 2: Publish and Verify the Release Manifest

**Files:**
- Create: `update-manifest.json`
- Modify: `README.md`
- Test: `tests/StaticUpdateManifestContractTests.ps1`

**Interfaces:**
- Consumes the exact installer metadata from the release build.
- Produces a public, checked-in manifest used by subsequent client versions.

- [ ] **Step 1: Write a failing manifest contract test**

```powershell
$manifest = Get-Content -Raw update-manifest.json | ConvertFrom-Json
if ($manifest.installerUrl -notmatch '^https://github\.com/.+/releases/download/v1\.1\.13/HuahaiClipboard-Setup\.exe$') {
    throw 'Static manifest installer URL is invalid.'
}
```

- [ ] **Step 2: Run it and verify it fails**

Run: `pwsh tests/StaticUpdateManifestContractTests.ps1`

Expected: `update-manifest.json` does not exist.

- [ ] **Step 3: Add the release manifest and documentation**

Write the exact released size and SHA-256 after the signed installer build. Document that the manifest must be updated with the release metadata before publishing the tag.

- [ ] **Step 4: Run manifest, Core, build, and installer verification**

Run: `pwsh tests/StaticUpdateManifestContractTests.ps1`

Run: `dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj -c Release`

Run: Visual Studio Build Tools x64 Release MSBuild command.

- [ ] **Step 5: Commit the release manifest**

Commit message: `release: publish static update manifest`
