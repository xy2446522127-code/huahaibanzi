# 花海剪贴板 Gate 0B/0C UI Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the production-quality, reusable WinUI 3 UI shell for 花海剪贴板 with mock data, all approved visual states, five themes, Liquid Glass, A+E click motion, background petals, tray navigation, and a deterministic Gate 0C preview.

**Architecture:** A pure `.NET 8` core project owns immutable models, UI-facing service contracts, filtering, and visual policy. A WinUI 3 app project owns windows, controls, Composition/Win2D rendering, tray integration, and mock adapters; real clipboard, privacy, persistence, and global-input adapters are explicitly excluded until Gate 0C approval. A pure MSTest project exercises core behavior, while deterministic scene arguments and visible Windows capture provide visual and interaction evidence.

**Tech Stack:** C# 12, .NET SDK `8.0.412`, WinUI 3, Windows App SDK `1.7.250606001`, Win2D `1.3.2`, MSTest `3.9.3`, Microsoft.NET.Test.Sdk `17.14.1`, Windows 10 SDK `10.0.19041.0`, Visual Studio 2022 Build Tools `17.14`.

## Global Constraints

- Product display name is `花海剪贴板`; executable names remain `HuahaiClipboard.exe` and `HuahaiClipboard-Setup.exe`.
- Target Windows 10 22H2 and currently supported Windows 11 versions; compile against `net8.0-windows10.0.19041.0` and test Windows 10 behavior as the reduced visual mode.
- Default theme is rose purple; built-in themes are rose purple, cobalt blue, emerald cyan, amber orange, and aurora cyan-purple.
- Liquid Glass must show desktop transparency, theme-colored edge refraction, moving colored reflections, bottom thickness, and dark content lenses; large white reflection patches are prohibited.
- A+E click feedback lasts `760ms`; reduced motion uses a `120ms` static highlight.
- Default petal density is five petals, each no wider than `7px`, placed only behind content lenses; levels are off, low, medium, and high.
- Primary text is white or near-white, secondary text is stable light gray, and all core controls have visible keyboard focus and accessible names.
- No account, cloud sync, telemetry upload, real clipboard capture, DPAPI payload storage, SQLite history, global mouse hook, automatic paste, or installer work is allowed in this plan.
- The UI shell uses mock adapters behind the same contracts that production adapters will implement after Gate 0C approval.
- Production dependency restore and Windows development-tool installation require explicit authorization at execution time.
- Do not push, deploy, publish, upload evidence, or configure an external update source.

## Scope And Plan Split

This plan stops at the Gate 0C approval boundary. The approved product specification is intentionally split into four independently verifiable implementation plans:

1. This plan: reusable UI shell and private local preview.
2. After Gate 0C approval: clipboard capture, privacy filtering, encrypted history, and settings persistence.
3. After core data contracts stabilize: global middle-click, hotkey, production edge positioning, per-monitor persistence, and automatic paste.
4. After integrated behavior passes: installer, startup integration, Windows 10/11 compatibility matrix, and release validation.

## File Map

```text
HuahaiClipboard.sln
Directory.Build.props
Directory.Packages.props
global.json
src/
  HuahaiClipboard.Core/
    HuahaiClipboard.Core.csproj
    Models/ClipboardRecord.cs
    Models/ClipboardFilter.cs
    Models/PanelActionResult.cs
    Settings/AppearanceSettings.cs
    Settings/MotionSettings.cs
    Settings/ShellSettings.cs
    Contracts/IClipboardHistorySource.cs
    Contracts/IPanelActionSink.cs
    Contracts/ISettingsStore.cs
    Contracts/IWindowNavigator.cs
    Presentation/ObservableObject.cs
    Presentation/PanelViewModel.cs
    Presentation/SettingsViewModel.cs
    Visual/ThemeDefinition.cs
    Visual/ThemeCatalog.cs
    Visual/VisualEnvironment.cs
    Visual/VisualMode.cs
    Visual/VisualModeResolver.cs
    Visual/MotionPolicy.cs
  HuahaiClipboard.App/
    HuahaiClipboard.App.csproj
    app.manifest
    App.xaml
    App.xaml.cs
    CompositionRoot.cs
    Assets/Brand/wordmark-a.png
    Assets/Brand/tray-mark.ico
    Infrastructure/Mocks/MockClipboardHistorySource.cs
    Infrastructure/Mocks/MockPanelActionSink.cs
    Infrastructure/Mocks/MemorySettingsStore.cs
    Infrastructure/Tray/TrayService.cs
    Presentation/Controls/BrandWordmark.xaml
    Presentation/Controls/ClipboardRecordRow.xaml
    Presentation/Controls/ClipboardRecordRow.xaml.cs
    Presentation/Controls/LiquidGlassSurface.xaml
    Presentation/Controls/LiquidGlassSurface.xaml.cs
    Presentation/Controls/PetalField.xaml
    Presentation/Controls/PetalField.xaml.cs
    Presentation/Effects/ClickFeedbackController.cs
    Presentation/Effects/LiquidGlassRenderer.cs
    Presentation/Theming/ThemeResourceMapper.cs
    Presentation/Windows/CursorPanelWindow.xaml
    Presentation/Windows/CursorPanelWindow.xaml.cs
    Presentation/Windows/EdgePanelWindow.xaml
    Presentation/Windows/EdgePanelWindow.xaml.cs
    Presentation/Windows/SettingsWindow.xaml
    Presentation/Windows/SettingsWindow.xaml.cs
    Presentation/Windows/SceneHostWindow.xaml
    Presentation/Windows/SceneHostWindow.xaml.cs
    Presentation/Windows/WindowNavigator.cs
    Resources/Brushes.xaml
    Resources/ControlStyles.xaml
    Resources/Typography.xaml
tests/
  HuahaiClipboard.Core.Tests/
    HuahaiClipboard.Core.Tests.csproj
    PanelViewModelTests.cs
    SettingsViewModelTests.cs
    ThemeCatalogTests.cs
    VisualModeResolverTests.cs
    UiResourceContractTests.cs
scripts/
  Start-UiScene.ps1
docs/product/
  ui-shell-control-contract.md
  ui-shell-acceptance.md
.codex/artifacts/ui-qa/  # ignored evidence only
```

### Task 1: Establish The Toolchain And Solution Boundary

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `HuahaiClipboard.sln`
- Create: `src/HuahaiClipboard.Core/HuahaiClipboard.Core.csproj`
- Create: `src/HuahaiClipboard.App/HuahaiClipboard.App.csproj`
- Create: `src/HuahaiClipboard.App/app.manifest`
- Create: `src/HuahaiClipboard.App/App.xaml`
- Create: `src/HuahaiClipboard.App/App.xaml.cs`
- Create: `tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj`

**Interfaces:**
- Consumes: approved product spec and visual-source version `1.0.0`.
- Produces: buildable solution with project references `App -> Core` and `Core.Tests -> Core`.

- [ ] **Step 1: Record the current failing prerequisite check**

Run:

```powershell
dotnet --list-sdks
Get-Command msbuild -ErrorAction SilentlyContinue
```

Expected before installation: no SDK rows and no `msbuild` command. Save the sanitized output in `.codex/artifacts/ui-qa/toolchain-before.txt`.

- [ ] **Step 2: Pause for explicit dependency and toolchain authorization**

Request authorization for these exact local installations and restores:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --version 8.0.412 --exact
winget install --id Microsoft.VisualStudio.2022.BuildTools --exact --override "--wait --passive --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.ComponentGroup.WindowsAppSDK.Cs --includeRecommended"
dotnet restore HuahaiClipboard.sln
```

Do not run these commands until the user grants permission in the execution session.

- [ ] **Step 3: Pin the SDK and package versions**

Create `global.json`:

```json
{
  "sdk": {
    "version": "8.0.412",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.WindowsAppSDK" Version="1.7.250606001" />
    <PackageVersion Include="Microsoft.Graphics.Win2D" Version="1.3.2" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="MSTest.TestAdapter" Version="3.9.3" />
    <PackageVersion Include="MSTest.TestFramework" Version="3.9.3" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create the three project files**

Create `HuahaiClipboard.Core.csproj` as a plain `net8.0` class library. Create `HuahaiClipboard.App.csproj` with these exact WinUI properties:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RootNamespace>HuahaiClipboard.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
    <RootNamespace>HuahaiClipboard.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <UseWinUI>true</UseWinUI>
    <UseWindowsForms>true</UseWindowsForms>
    <WindowsPackageType>None</WindowsPackageType>
    <Platforms>x64</Platforms>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <PackageReference Include="Microsoft.Graphics.Win2D" />
    <ProjectReference Include="..\HuahaiClipboard.Core\HuahaiClipboard.Core.csproj" />
  </ItemGroup>
</Project>
```

Create the MSTest project with `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`, and a project reference to Core:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <RootNamespace>HuahaiClipboard.Core.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="MSTest.TestAdapter" />
    <PackageReference Include="MSTest.TestFramework" />
    <ProjectReference Include="..\..\src\HuahaiClipboard.Core\HuahaiClipboard.Core.csproj" />
  </ItemGroup>
</Project>
```

Create `app.manifest` with per-monitor V2 DPI awareness and long-path awareness:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="HuahaiClipboard.App" />
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </windowsSettings>
  </application>
</assembly>
```

Add the minimal WinUI application bootstrap so Task 1 is genuinely buildable:

```xml
<Application
    x:Class="HuahaiClipboard.App.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources />
</Application>
```

```csharp
namespace HuahaiClipboard.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    public App() => InitializeComponent();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
    }
}
```

- [ ] **Step 5: Add projects to the solution and verify the dependency graph**

Run:

```powershell
dotnet new sln --name HuahaiClipboard
dotnet sln HuahaiClipboard.sln add src/HuahaiClipboard.Core/HuahaiClipboard.Core.csproj
dotnet sln HuahaiClipboard.sln add src/HuahaiClipboard.App/HuahaiClipboard.App.csproj
dotnet sln HuahaiClipboard.sln add tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj
dotnet list src/HuahaiClipboard.App/HuahaiClipboard.App.csproj reference
dotnet list tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj reference
```

Expected: App references Core; Core.Tests references Core; Core references no app or Windows UI assembly.

- [ ] **Step 6: Verify restore and build**

Run:

```powershell
dotnet restore HuahaiClipboard.sln
dotnet build HuahaiClipboard.sln -c Debug -p:Platform=x64 --no-restore
```

Expected: exit code `0`, zero warnings, zero errors.

- [ ] **Step 7: Create the task checkpoint**

Run the App Product Delivery checkpoint command with every Task 1 owned path and validations `restore passed` and `build passed`. Use commit subject:

```text
build: establish WinUI solution
```

### Task 2: Define Core Models, Contracts, And Mock Data

**Files:**
- Create: `src/HuahaiClipboard.Core/Models/ClipboardRecord.cs`
- Create: `src/HuahaiClipboard.Core/Models/ClipboardFilter.cs`
- Create: `src/HuahaiClipboard.Core/Models/PanelActionResult.cs`
- Create: `src/HuahaiClipboard.Core/Contracts/IClipboardHistorySource.cs`
- Create: `src/HuahaiClipboard.Core/Contracts/IPanelActionSink.cs`
- Create: `src/HuahaiClipboard.Core/Contracts/IWindowNavigator.cs`
- Create: `src/HuahaiClipboard.App/Infrastructure/Mocks/MockClipboardHistorySource.cs`
- Create: `src/HuahaiClipboard.App/Infrastructure/Mocks/MockPanelActionSink.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/PanelViewModelTests.cs`

**Interfaces:**
- Consumes: Core project from Task 1.
- Produces: `ClipboardRecord`, `IClipboardHistorySource`, `IPanelActionSink`, and `IWindowNavigator` contracts used by every window and panel view model.

- [ ] **Step 1: Write failing model and contract tests**

Add tests asserting that records preserve type, favorite, pin, unavailable-file state, and stable IDs, and that the mock source returns text, link, image, file, favorite, pinned, empty, and unavailable examples.

```csharp
[TestMethod]
public void ClipboardRecord_PreservesEveryApprovedRecordState()
{
    var record = new ClipboardRecord(
        Guid.Parse("00000000-0000-0000-0000-000000000004"),
        ClipboardItemKind.File,
        @"C:\\资料\\花海.txt",
        "文件不可用",
        DateTimeOffset.Parse("2026-08-04T09:00:00+08:00"),
        IsFavorite: true,
        IsPinned: true,
        IsAvailable: false,
        PreviewAssetPath: null);
    Assert.AreEqual(ClipboardItemKind.File, record.Kind);
    Assert.IsTrue(record.IsFavorite);
    Assert.IsTrue(record.IsPinned);
    Assert.IsFalse(record.IsAvailable);
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter ClipboardRecord_PreservesEveryApprovedRecordState
```

Expected: compile failure because the model and mock source do not exist.

- [ ] **Step 3: Implement immutable records and explicit action results**

Use these exact public shapes:

```csharp
public enum ClipboardItemKind { Text, Link, Image, File }
public enum ClipboardFilter { All, Text, Link, Image, File, Favorites }

public sealed record ClipboardRecord(
    Guid Id,
    ClipboardItemKind Kind,
    string PrimaryText,
    string SecondaryText,
    DateTimeOffset LastCopiedAt,
    bool IsFavorite,
    bool IsPinned,
    bool IsAvailable,
    string? PreviewAssetPath);

public sealed record PanelActionResult(bool Succeeded, string? RecoveryMessage)
{
    public static PanelActionResult Success() => new(true, null);
    public static PanelActionResult Failure(string message) => new(false, message);
}
```

Define contracts:

```csharp
public interface IClipboardHistorySource
{
    Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken);
    Task SetFavoriteAsync(Guid recordId, bool value, CancellationToken cancellationToken);
    Task SetPinnedAsync(Guid recordId, bool value, CancellationToken cancellationToken);
    Task DeleteAsync(Guid recordId, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public interface IPanelActionSink
{
    Task<PanelActionResult> CopyAsync(Guid recordId, CancellationToken cancellationToken);
    Task<PanelActionResult> PasteAsync(Guid recordId, CancellationToken cancellationToken);
}

public interface IWindowNavigator
{
    void ShowCursorPanel();
    void ShowEdgePanel();
    void ShowSettings();
    void HideTransientPanel();
}
```

- [ ] **Step 4: Implement deterministic mock adapters**

The history source must return exactly 12 stable records with fixed GUIDs and timestamps anchored to `2026-08-04T09:00:00+08:00`. `MockPanelActionSink` returns success except for record ID `00000000-0000-0000-0000-000000000012`, which returns `PanelActionResult.Failure("已复制，请按 Ctrl+V 手动粘贴")` for the visible recovery state.

- [ ] **Step 5: Run focused and project tests**

Run:

```powershell
dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj -c Debug
```

Expected: all tests pass with zero skipped tests.

- [ ] **Step 6: Create the task checkpoint**

Checkpoint the listed model, contract, mock, and test files with:

```text
feat: define UI shell contracts
```

### Task 3: Implement Theme, Motion, And Environment Policies

**Files:**
- Create: `src/HuahaiClipboard.Core/Settings/AppearanceSettings.cs`
- Create: `src/HuahaiClipboard.Core/Settings/MotionSettings.cs`
- Create: `src/HuahaiClipboard.Core/Settings/ShellSettings.cs`
- Create: `src/HuahaiClipboard.Core/Visual/ThemeDefinition.cs`
- Create: `src/HuahaiClipboard.Core/Visual/ThemeCatalog.cs`
- Create: `src/HuahaiClipboard.Core/Visual/VisualEnvironment.cs`
- Create: `src/HuahaiClipboard.Core/Visual/VisualMode.cs`
- Create: `src/HuahaiClipboard.Core/Visual/VisualModeResolver.cs`
- Create: `src/HuahaiClipboard.Core/Contracts/ISettingsStore.cs`
- Create: `src/HuahaiClipboard.App/Infrastructure/Mocks/MemorySettingsStore.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/ThemeCatalogTests.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/VisualModeResolverTests.cs`

**Interfaces:**
- Consumes: no Windows UI types.
- Produces: five immutable `ThemeDefinition` values, deterministic `VisualModeResolver.Resolve(VisualEnvironment)` behavior, and the settings-store contract used by Task 4.

- [ ] **Step 1: Write failing tests for all visual contracts**

```csharp
[TestMethod]
public void Catalog_ContainsExactlyFiveNamedThemes()
{
    CollectionAssert.AreEqual(
        new[] { "rose-purple", "cobalt-blue", "emerald-cyan", "amber-orange", "aurora-cyan-purple" },
        ThemeCatalog.All.Select(theme => theme.Id).ToArray());
}

[TestMethod]
public void Resolver_UsesReducedModeOnWindows10()
{
    var environment = new VisualEnvironment(IsWindows11: false, IsHighContrast: false, IsReducedMotion: false, IsRemoteSession: false, IsEnergySaver: false);
    Assert.AreEqual(VisualMode.Reduced, VisualModeResolver.Resolve(environment));
}

[TestMethod]
public void Resolver_UsesStaticModeForAccessibilityOrConstrainedSessions()
{
    Assert.AreEqual(VisualMode.Static, VisualModeResolver.Resolve(new(true, true, false, false, false)));
    Assert.AreEqual(VisualMode.Static, VisualModeResolver.Resolve(new(true, false, true, false, false)));
    Assert.AreEqual(VisualMode.Static, VisualModeResolver.Resolve(new(true, false, false, true, false)));
    Assert.AreEqual(VisualMode.Static, VisualModeResolver.Resolve(new(true, false, false, false, true)));
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj --filter "ThemeCatalogTests|VisualModeResolverTests"
```

Expected: compile failure for undefined visual types.

- [ ] **Step 3: Implement the settings records**

```csharp
public sealed record AppearanceSettings(
    string ThemeId,
    double Opacity,
    double BlurAmount,
    double ReflectionStrength,
    bool CompactMode);

public enum PetalLevel { Off, Low, Medium, High }

public sealed record MotionSettings(
    PetalLevel PetalLevel,
    bool ReduceMotion,
    int ClickDurationMs = 760,
    int ReducedClickDurationMs = 120);

public sealed record ShellSettings(AppearanceSettings Appearance, MotionSettings Motion)
{
    public static ShellSettings Default => new(
        new("rose-purple", 0.86, 32, 0.72, false),
        new(PetalLevel.Low, false));
}
```

Define the settings storage boundary after `ShellSettings` exists:

```csharp
public interface ISettingsStore
{
    Task<ShellSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(ShellSettings settings, CancellationToken cancellationToken);
}
```

`MemorySettingsStore` starts from `ShellSettings.Default`, returns immutable snapshots, and serializes writes through `SemaphoreSlim` so rapid slider changes cannot reorder preview state.

- [ ] **Step 4: Implement exact theme tokens**

`ThemeDefinition` contains `Id`, `DisplayName`, `Accent`, `Reflection`, `GlassTop`, `GlassBottom`, `ContentLens`, `FocusBorder`, `TextPrimary`, and `TextSecondary` as eight-digit ARGB strings. Use these accent/reflection pairs:

```text
rose-purple:        #FFE9A6D1 / #FFBC7CAF
cobalt-blue:        #FF77B5FF / #FF4277D4
emerald-cyan:       #FF65DEC8 / #FF289B91
amber-orange:       #FFFFC26D / #FFD57942
aurora-cyan-purple: #FF7FE8E0 / #FF9B7DE3
```

Every theme uses `#FFFFFFFF` primary text and `#CCFFFFFF` secondary text. Content lenses remain darker than `#B34C344F` in relative luminance.

- [ ] **Step 5: Implement the visual mode resolver**

```csharp
public enum VisualMode { Full, Reduced, Static }

public sealed record VisualEnvironment(
    bool IsWindows11,
    bool IsHighContrast,
    bool IsReducedMotion,
    bool IsRemoteSession,
    bool IsEnergySaver);

public static class VisualModeResolver
{
    public static VisualMode Resolve(VisualEnvironment environment)
    {
        if (environment.IsHighContrast || environment.IsReducedMotion || environment.IsRemoteSession || environment.IsEnergySaver)
            return VisualMode.Static;
        return environment.IsWindows11 ? VisualMode.Full : VisualMode.Reduced;
    }
}
```

- [ ] **Step 6: Run all core tests and checkpoint**

Run the full Core.Tests project. Checkpoint only the settings, visual policy, and test files with:

```text
feat: define visual theme policies
```

### Task 4: Build The Panel View Model And Interaction State Machine

**Files:**
- Create: `src/HuahaiClipboard.Core/Presentation/ObservableObject.cs`
- Create: `src/HuahaiClipboard.Core/Presentation/PanelViewModel.cs`
- Create: `src/HuahaiClipboard.Core/Presentation/SettingsViewModel.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/PanelViewModelTests.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `IClipboardHistorySource`, `IPanelActionSink`, `ISettingsStore`, and `IWindowNavigator`.
- Produces: bindable panel/search/filter/selection/action state and bindable settings/live-preview state without WinUI dependencies.

- [ ] **Step 1: Write failing filter and action tests**

```csharp
[TestMethod]
public async Task SearchAndFilter_ComposeWithoutChangingSourceOrder()
{
    var viewModel = TestPanelFactory.Create();
    await viewModel.LoadAsync();
    viewModel.SelectedFilter = ClipboardFilter.Link;
    viewModel.SearchText = "openai";
    Assert.IsTrue(viewModel.VisibleRecords.All(record => record.Kind == ClipboardItemKind.Link));
    Assert.IsTrue(viewModel.VisibleRecords.All(record => record.PrimaryText.Contains("openai", StringComparison.OrdinalIgnoreCase)));
}

[TestMethod]
public async Task CopySuccess_HidesTransientPanel()
{
    var fixture = TestPanelFactory.CreateWithNavigator();
    await fixture.ViewModel.LoadAsync();
    await fixture.ViewModel.CopyAsync(fixture.ViewModel.VisibleRecords[0]);
    Assert.AreEqual(1, fixture.Navigator.HideTransientPanelCalls);
    Assert.IsNull(fixture.ViewModel.RecoveryMessage);
}

[TestMethod]
public async Task PasteFailure_LeavesRecoveryMessageVisible()
{
    var fixture = TestPanelFactory.CreateWithFailingPaste();
    await fixture.ViewModel.LoadAsync();
    await fixture.ViewModel.PasteAsync(fixture.FailingRecord);
    Assert.AreEqual("已复制，请按 Ctrl+V 手动粘贴", fixture.ViewModel.RecoveryMessage);
    Assert.AreEqual(0, fixture.Navigator.HideTransientPanelCalls);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Expected: compile failure because `PanelViewModel` and test fixtures do not exist.

- [ ] **Step 3: Implement explicit panel states**

`PanelViewModel` exposes `AllRecords`, `VisibleRecords`, `SearchText`, `SelectedFilter`, `SelectedRecord`, `IsLoading`, `IsEmpty`, `IsBusy`, and `RecoveryMessage`. It implements `LoadAsync`, `CopyAsync`, `PasteAsync`, `ToggleFavoriteAsync`, `TogglePinnedAsync`, `DeleteAsync`, `MoveSelection(int delta)`, and `Close()`.

Filtering rules are exact: trim search whitespace, compare `PrimaryText` and `SecondaryText` with `OrdinalIgnoreCase`, apply the selected type or favorites filter, then sort pinned first and `LastCopiedAt` descending without mutating `AllRecords`.

- [ ] **Step 4: Implement settings live preview**

`SettingsViewModel` exposes `Draft`, `Themes`, `PetalLevels`, `SaveStatus`, `UpdateAppearanceAsync`, `UpdateMotionAsync`, and `ResetAppearanceAsync`. Every update writes through `ISettingsStore` and raises one `PreviewChanged` event containing the full `ShellSettings` snapshot.

- [ ] **Step 5: Run all tests and checkpoint**

Expected: all Core.Tests pass. Checkpoint the view models and tests with:

```text
feat: implement UI shell view models
```

### Task 5: Create The Cursor Panel, Record Rows, And Brand Placement

**Files:**
- Modify: `src/HuahaiClipboard.App/App.xaml`
- Modify: `src/HuahaiClipboard.App/App.xaml.cs`
- Create: `src/HuahaiClipboard.App/CompositionRoot.cs`
- Create: `src/HuahaiClipboard.App/Resources/Brushes.xaml`
- Create: `src/HuahaiClipboard.App/Resources/ControlStyles.xaml`
- Create: `src/HuahaiClipboard.App/Resources/Typography.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Controls/BrandWordmark.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Controls/ClipboardRecordRow.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Controls/ClipboardRecordRow.xaml.cs`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml.cs`
- Create: `src/HuahaiClipboard.App/Assets/Brand/wordmark-a.png`
- Test: `tests/HuahaiClipboard.Core.Tests/UiResourceContractTests.cs`

**Interfaces:**
- Consumes: `PanelViewModel`, approved `huahai-wordmark-integrated-v15.html`, and mock adapters.
- Produces: a reusable record list and a cursor panel window with real search, filtering, selection, favorite, pin, delete, copy, paste, settings, close, and recovery behavior.

- [ ] **Step 1: Add a failing XAML resource smoke check**

Add an MSTest that parses the three resource dictionaries as XML and asserts that each required key occurs exactly once:

```csharp
[TestMethod]
public void ResourceKeys_ArePresentExactlyOnce()
{
    string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    string[] files =
    [
        Path.Combine(repositoryRoot, "src", "HuahaiClipboard.App", "Resources", "Brushes.xaml"),
        Path.Combine(repositoryRoot, "src", "HuahaiClipboard.App", "Resources", "ControlStyles.xaml"),
        Path.Combine(repositoryRoot, "src", "HuahaiClipboard.App", "Resources", "Typography.xaml")
    ];
    string combined = string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    string[] requiredKeys =
    [
        "HuahaiTextPrimaryBrush",
        "HuahaiTextSecondaryBrush",
        "HuahaiContentLensBrush",
        "HuahaiFocusBrush",
        "HuahaiRecordRowStyle"
    ];
    foreach (string key in requiredKeys)
        Assert.AreEqual(1, Regex.Matches(combined, $"x:Key=\"{Regex.Escape(key)}\"").Count, key);
}
```

Expected RED: resource files do not exist.

- [ ] **Step 2: Generate the wordmark bitmap from the approved source**

Render the A wordmark from `.superpowers/brainstorm/visual-companion-1/content/huahai-wordmark-integrated-v15.html` at `3x` scale with transparent background. Crop only the approved mark, save it as `wordmark-a.png`, and verify its alpha channel. Do not redraw or reinterpret the logo.

- [ ] **Step 3: Build the resource dictionaries**

Set panel content typography to `Microsoft YaHei UI`, `14px` primary, `12px` secondary, and `22px` compact headings. Use zero letter spacing. Define stable row height `52px`, panel width `388px`, panel maximum height `620px`, search height `40px`, and icon button size `32px`.

- [ ] **Step 4: Build the cursor panel composition**

The visual tree order must be:

```text
Window root
  LiquidGlassSurface
    PetalField (not hit-testable)
    Decorative refraction layer (not hit-testable)
    Content grid
      BrandWordmark + gear + close
      Search box
      Type filter tabs
      Recovery InfoBar
      Record list or loading/empty/error state
```

Use a border radius no larger than `8px` for record rows and search surfaces. Buttons use built-in symbolic icons with tooltips and `AutomationProperties.Name`; no decorative text buttons replace familiar symbols.

- [ ] **Step 5: Wire every visible control**

Search updates `SearchText`; type tabs update `SelectedFilter`; single click awaits `CopyAsync`; double click and `Enter` await `PasteAsync`; star and pin buttons update the item; delete removes it; gear calls `ShowSettings`; close and `Esc` call `Close`. Up/down keys call `MoveSelection(-1/+1)`. While `IsBusy` is true, item actions are disabled and expose busy state.

- [ ] **Step 6: Run build and visible smoke test**

Run:

```powershell
dotnet build HuahaiClipboard.sln -c Debug -p:Platform=x64 --no-restore
Start-Process -FilePath 'src\HuahaiClipboard.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\HuahaiClipboard.App.exe' -ArgumentList '--scene cursor-default'
```

Verify search, filters, keyboard focus, copy hide, failed paste recovery, gear, and close in the visible app. Capture console/runtime exceptions; zero unhandled exceptions are accepted.

- [ ] **Step 7: Checkpoint the panel shell**

Use commit subject:

```text
feat: build cursor panel shell
```

### Task 6: Implement Liquid Glass And Five Theme Variants

**Files:**
- Create: `src/HuahaiClipboard.App/Presentation/Controls/LiquidGlassSurface.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Controls/LiquidGlassSurface.xaml.cs`
- Create: `src/HuahaiClipboard.App/Presentation/Effects/LiquidGlassRenderer.cs`
- Create: `src/HuahaiClipboard.App/Presentation/Theming/ThemeResourceMapper.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Windows/CursorPanelWindow.xaml`
- Test: `tests/HuahaiClipboard.Core.Tests/ThemeCatalogTests.cs`

**Interfaces:**
- Consumes: `ThemeDefinition`, `VisualMode`, appearance opacity/blur/reflection settings.
- Produces: one shared material surface used by cursor, edge, settings, and scene windows.

- [ ] **Step 1: Add failing theme mapping tests**

Test that every catalog entry defines all nine required token values and that primary/secondary text contrast against the content lens is at least `7:1` and `4.5:1` respectively using WCAG relative luminance math. UI resource mapping is verified by the resource smoke test from Task 5 and the visible five-theme matrix.

- [ ] **Step 2: Implement one renderer with three modes**

`LiquidGlassRenderer.ApplyAsync(LiquidGlassSurface surface, ThemeDefinition theme, AppearanceSettings appearance, VisualMode mode)` must:

- Full: use transparent window backdrop, `32px` effective blur, theme-colored edge refraction, a slow colored reflection strip, bottom thickness, and grain below `3%` opacity.
- Reduced: preserve layout and content lenses, cap blur at `18px`, remove displacement/refraction animation, and reduce saturation by `25%`.
- Static: use a stable theme-tinted translucent fill, visible focus borders, no animated shader, and no backdrop dependency.

The renderer owns disposable Win2D resources and releases them on window close or device loss.

- [ ] **Step 3: Implement the liquid surface input contract**

Expose dependency properties `Theme`, `Appearance`, and `VisualMode`. Clamp opacity to `0.65..0.96`, blur to `12..48`, and reflection strength to `0..1`. Invalid values fall back to `ShellSettings.Default` and never crash XAML loading.

- [ ] **Step 4: Verify all five themes at the same scene**

Launch `cursor-default` five times with `--theme` set to each ID. Capture identical `388x620` client-area screenshots. Confirm the theme changes accent/reflection/focus colors without changing layout, typography, content lens opacity, or control positions.

- [ ] **Step 5: Checkpoint material and themes**

Use commit subject:

```text
feat: render liquid glass themes
```

### Task 7: Implement A+E Click Motion And Background Petals

**Files:**
- Create: `src/HuahaiClipboard.App/Presentation/Controls/PetalField.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Controls/PetalField.xaml.cs`
- Create: `src/HuahaiClipboard.App/Presentation/Effects/ClickFeedbackController.cs`
- Create: `src/HuahaiClipboard.Core/Visual/MotionPolicy.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Controls/ClipboardRecordRow.xaml.cs`
- Modify: `src/HuahaiClipboard.App/Presentation/Controls/LiquidGlassSurface.xaml`
- Test: `tests/HuahaiClipboard.Core.Tests/VisualModeResolverTests.cs`

**Interfaces:**
- Consumes: selected theme, `MotionSettings`, `VisualMode`.
- Produces: `MotionPolicy.GetPetalCount(PetalLevel, VisualMode)`, `MotionPolicy.GetClickDurationMs(MotionSettings, VisualMode)`, interruptible click feedback, and deterministic petal fields with no layout or hit-test impact.

- [ ] **Step 1: Add failing policy tests**

Test exact mappings:

```csharp
Dictionary<PetalLevel, int> expectedCounts = new()
{
    [PetalLevel.Off] = 0,
    [PetalLevel.Low] = 5,
    [PetalLevel.Medium] = 9,
    [PetalLevel.High] = 14
};
```

Assert `Static` mode always returns zero petals and `120ms` feedback; `Full` and `Reduced` preserve the selected petal count unless the environment resolver returns `Static`.

Implement these exact signatures in `MotionPolicy`:

```csharp
public static int GetPetalCount(PetalLevel level, VisualMode mode);
public static int GetClickDurationMs(MotionSettings settings, VisualMode mode);
```

- [ ] **Step 2: Implement A+E feedback**

On row activation, run one `760ms` composition batch: a theme-colored refraction ring expands from pointer or keyboard activation center while the row translates from `0` to `-3px` and returns with a spring easing. The type icon translates from `0` to `-2px` and returns. A new activation cancels and replaces the current batch. Reduced motion shows a `120ms` accent border without translation or ripple.

- [ ] **Step 3: Implement background-only petals**

Use a seeded pseudo-random generator with scene seed `20260804`. Petal width is clamped to `5..7px`; vertical duration is `14..24s`; horizontal drift is at most `18px`; opacity is `0.18..0.38`. Set `IsHitTestVisible="False"`, place the control before every content lens in XAML, and clip it to the glass surface.

- [ ] **Step 4: Capture deterministic motion evidence**

Capture initial, midpoint (`380ms`), and final (`760ms`) click frames plus a short recording. Capture low, medium, high, off, and reduced-motion petal states. Verify row bounds and text coordinates are identical across frames.

- [ ] **Step 5: Checkpoint motion**

Use commit subject:

```text
feat: add liquid click and petal motion
```

### Task 8: Build Edge Panel, Settings, Tray, And Scene Navigation

**Files:**
- Create: `src/HuahaiClipboard.App/Presentation/Windows/EdgePanelWindow.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/EdgePanelWindow.xaml.cs`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/SettingsWindow.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/SettingsWindow.xaml.cs`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/WindowNavigator.cs`
- Create: `src/HuahaiClipboard.App/Infrastructure/Tray/TrayService.cs`
- Create: `src/HuahaiClipboard.App/Assets/Brand/tray-mark.ico`
- Modify: `src/HuahaiClipboard.App/CompositionRoot.cs`
- Modify: `src/HuahaiClipboard.App/App.xaml.cs`
- Test: `tests/HuahaiClipboard.Core.Tests/SettingsViewModelTests.cs`

**Interfaces:**
- Consumes: shared `LiquidGlassSurface`, `ClipboardRecordRow`, `PanelViewModel`, `SettingsViewModel`, and `IWindowNavigator`.
- Produces: all approved shell entry and exit paths, including real tray commands operating against mock state.

- [ ] **Step 1: Write failing settings persistence and navigation tests**

Test that changing theme, opacity, blur, reflection, compact mode, petal level, and reduced motion produces one complete saved snapshot and one preview event. Tray navigation is verified through visible integration in Step 5 because the `NotifyIcon` adapter belongs to the Windows UI process rather than Core.

- [ ] **Step 2: Build the edge panel**

Reuse the panel content control; do not duplicate record-list XAML. Provide a stable `18px` vertical theme-colored edge handle, right-side default, left/right preview states, expand/collapse, and drag preview. The mock implementation stores side and vertical position in `MemorySettingsStore`; real per-monitor persistence remains excluded.

- [ ] **Step 3: Build the settings window**

Use an unframed two-column layout with sections: appearance, motion, input, clipboard, privacy, behavior, displays, and system. Appearance and motion controls are functional against the mock store and update all open shell windows immediately. System-dependent controls are disabled and expose these concise reasons through `ToolTipService.ToolTip` and `AutomationProperties.HelpText`, not permanent instructional copy:

```text
中键全局监听：UI 外壳确认后接入
真实剪贴板记录：UI 外壳确认后接入
开机启动：安装阶段接入
```

Disabled controls cannot receive pointer or keyboard activation. Every section remains reachable and escapable by keyboard.

- [ ] **Step 4: Build the real tray navigation surface**

Use `System.Windows.Forms.NotifyIcon` with the approved independent 花 icon. Menu text is exactly: `显示面板`, `暂停记录`, `隐私模式`, `清空历史`, `设置`, `退出`. Commands operate on mock state: show windows, toggle checkmarks, confirm and clear mock history, or exit. Dispose the tray icon exactly once during application shutdown.

- [ ] **Step 5: Verify all entry and exit paths**

From the tray, open cursor panel, edge panel, and settings. From both panels, open settings via the gear. Verify close, `Esc`, tray exit, and clear-history confirmation. Verify no window is unreachable or traps focus.

- [ ] **Step 6: Checkpoint shell navigation**

Use commit subject:

```text
feat: complete shell navigation surfaces
```

### Task 9: Add Deterministic Scenes, Accessibility, And UI Evidence

**Files:**
- Create: `src/HuahaiClipboard.App/Presentation/Windows/SceneHostWindow.xaml`
- Create: `src/HuahaiClipboard.App/Presentation/Windows/SceneHostWindow.xaml.cs`
- Create: `scripts/Start-UiScene.ps1`
- Create: `docs/product/ui-shell-control-contract.md`
- Create: `docs/product/ui-shell-acceptance.md`
- Modify: `src/HuahaiClipboard.App/App.xaml.cs`
- Modify: all window and control XAML files created in Tasks 5-8

**Interfaces:**
- Consumes: every shared UI component and mock adapter.
- Produces: reproducible route/state/theme arguments and evidence inventory for Gate 0C.

- [ ] **Step 1: Implement exact scene arguments**

Support:

```text
--scene cursor-default|cursor-empty|cursor-error|cursor-recovery|edge-right|edge-left|settings-appearance|settings-motion|settings-privacy
--theme rose-purple|cobalt-blue|emerald-cyan|amber-orange|aurora-cyan-purple
--visual-mode full|reduced|static
--motion normal|reduced
--scale 100|150|200
```

Unknown values exit with code `2` and write a sanitized error that lists accepted values. Scene data is fixed; no date, locale, network, clipboard, or filesystem state may change a rendered scene.

- [ ] **Step 2: Create the scene launcher**

`Start-UiScene.ps1` validates arguments and launches the built executable without constructing a shell command string:

```powershell
param(
  [Parameter(Mandatory)][ValidateSet('cursor-default','cursor-empty','cursor-error','cursor-recovery','edge-right','edge-left','settings-appearance','settings-motion','settings-privacy')][string]$Scene,
  [ValidateSet('rose-purple','cobalt-blue','emerald-cyan','amber-orange','aurora-cyan-purple')][string]$Theme = 'rose-purple',
  [ValidateSet('full','reduced','static')][string]$VisualMode = 'full',
  [ValidateSet('normal','reduced')][string]$Motion = 'normal'
)
$exe = Join-Path $PSScriptRoot '..\src\HuahaiClipboard.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\HuahaiClipboard.App.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Build output not found: $exe" }
Start-Process -FilePath $exe -ArgumentList @('--scene', $Scene, '--theme', $Theme, '--visual-mode', $VisualMode, '--motion', $Motion) -PassThru
```

- [ ] **Step 3: Inventory every visible control**

Write `ui-shell-control-contract.md` with one row per visible control and columns: `Surface`, `Control`, `Accessible name`, `Trigger`, `Action`, `Loading`, `Success`, `Disabled`, `Error`, `Keyboard`, `Evidence`. Remove, implement, or intentionally disable every control that lacks a real mock action.

- [ ] **Step 4: Run accessibility checks**

Verify keyboard focus order, `Enter`, `Space`, arrow keys, `Esc`, visible focus, high contrast, 100/150/200% scale, Chinese long text, reduced motion, and screen-reader names. Measure primary and secondary text contrast against actual content-lens colors. Acceptance is `>=7:1` primary and `>=4.5:1` secondary.

- [ ] **Step 5: Capture the required evidence matrix**

Store ignored artifacts under `.codex/artifacts/ui-qa/gate-0c/`:

```text
reference/approved-v1/
implementation/rose-purple/
implementation/five-themes/
implementation/windows10-reduced/
implementation/high-contrast/
motion/click-ae/
motion/petals/
diff/
```

Capture the same viewport, scale, theme, scene, and mock data for reference, implementation, and difference images. Record click initial/mid/final frames and a short motion recording. Use visible Windows app interaction; screenshots alone do not satisfy the control contract.

- [ ] **Step 6: Run the integrated shell gate**

Run:

```powershell
dotnet test tests/HuahaiClipboard.Core.Tests/HuahaiClipboard.Core.Tests.csproj -c Release
dotnet build HuahaiClipboard.sln -c Release -p:Platform=x64 --no-restore
git diff --check
```

Then exercise every control-contract row in the visible app and record actual versus expected results in `docs/product/ui-shell-acceptance.md`. No P0/P1 issue, dead control, missing exit path, foreground petal, large white reflection patch, or low-contrast text is accepted.

- [ ] **Step 7: Checkpoint deterministic preview and evidence metadata**

Checkpoint source and documentation, never the ignored screenshots or recordings, with:

```text
test: add deterministic UI shell acceptance
```

### Task 10: Present Gate 0C And Record The Approval Boundary

**Files:**
- Create after explicit approval: `.codex/app-product-delivery-visual-contract.json`
- Modify after explicit approval: `.codex/app-product-delivery-visual-source.json`
- Modify after explicit approval: `.codex/app-product-delivery-coordination.json`

**Interfaces:**
- Consumes: verified UI shell checkpoint and ignored Gate 0C evidence.
- Produces: the approved visual-contract version that authorizes production adapters to replace mock adapters.

- [ ] **Step 1: Present the complete local preview**

Provide the executable launch command, commit hash, visual source version, scene inventory, five-theme matrix, Windows 10 reduced-mode comparison, accessibility results, motion evidence, control-contract result, known P2/P3 differences, and rollback commit. Do not describe the shell as functional product completion.

- [ ] **Step 2: Pause for explicit Gate 0C approval**

Ask one question: approve this exact shared UI shell, request derivative visual changes, or reject and return to the approved source. Do not connect real clipboard data, global hooks, persistence, or automatic paste before the user explicitly approves.

- [ ] **Step 3: Record approval from deterministic data**

After approval, generate `.codex/app-product-delivery-visual-contract.json` from the current repository state and evidence hashes with this script:

```powershell
$contract = [ordered]@{
  version = 1
  status = 'gate-0c-approved'
  visual_source_version = '1.0.0'
  approved_shell_commit = (git rev-parse HEAD).Trim()
  approved_at_utc = (Get-Date).ToUniversalTime().ToString('o')
  surfaces = @('cursor-default','cursor-empty','cursor-error','cursor-recovery','edge-right','edge-left','settings-appearance','settings-motion','settings-privacy')
  themes = @('rose-purple','cobalt-blue','emerald-cyan','amber-orange','aurora-cyan-purple')
  evidence_root = '.codex/artifacts/ui-qa/gate-0c/'
}
$json = $contract | ConvertTo-Json -Depth 4
$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path (Get-Location) '.codex/app-product-delivery-visual-contract.json'), $json + [Environment]::NewLine, $utf8)
```

- [ ] **Step 4: Validate and checkpoint approval metadata**

Run UTF-8 JSON parsing, verify every evidence path exists and remains ignored, advance the coordination manifest with contract `visual-shell=1.0.0`, and checkpoint only the three `.codex` files with:

```text
chore: approve Gate 0C visual shell
```

## Spec Coverage Review

| Product spec area | This plan's coverage | Boundary |
|---|---|---|
| Cursor panel flow | Tasks 2, 4, 5, and 9 implement search, filter, keyboard selection, mock copy/paste, hide, and recovery states | Global middle-click, real clipboard write, foreground restoration, and synthetic paste wait for the production-input plan |
| Edge panel | Task 8 implements the shared content, left/right states, handle, expand/collapse, and drag preview | Real monitor enumeration, DPI persistence, and screen-edge activation wait for the production-input plan |
| Settings and tray | Tasks 3, 4, and 8 implement all sections, live visual preview, mock toggles, clear confirmation, navigation, and exit | DPAPI/settings files, startup registration, real privacy mode, and application exclusions wait for production plans |
| History information architecture | Tasks 2, 4, and 5 cover text, link, image, file, favorites, pins, missing files, loading, empty, error, and recovery states | Capture, deduplication, retention, SQLite, and encrypted payloads remain excluded |
| Brand and five themes | Tasks 3, 5, 6, and 9 derive from visual source `1.0.0` and verify identical layouts | No alternate visual interpretation is authorized |
| Liquid Glass and accessibility | Tasks 3, 6, 7, and 9 cover full/reduced/static modes, contrast, focus, scale, and reduced motion | Windows 10/11 production compatibility is rechecked after real adapters are integrated |
| A+E and petals | Task 7 implements exact duration, size, density, layer, interruption, and fallback contracts | Decorative motion never receives input or changes layout |
| Architecture | Tasks 1-4 establish Core contracts and replaceable mock adapters | Clipboard, privacy, storage, global input, installer, and updater production logic is prohibited before Gate 0C approval |
| Visual acceptance | Tasks 9 and 10 create deterministic scenes, before/after/difference evidence, motion evidence, control inventory, and the approval record | Screenshots alone never count as interaction proof |

## Final Acceptance For This Plan

- The WinUI shell builds for `x64` with zero warnings and starts on the available Windows test machine.
- Core tests pass with zero failures and zero skipped tests.
- Cursor panel, edge panel, settings window, and tray are reachable and escapable.
- Every visible control acts on mock state or is visibly disabled with a reason.
- Five themes preserve identical layout and pass text contrast targets.
- Liquid Glass is materially visible without large white reflection patches.
- A+E click motion and background petals match the approved durations, sizes, layers, and reduced-motion behavior.
- Windows 10 reduced mode and static accessibility mode preserve layout and readability.
- Reference, implementation, difference, motion, keyboard, scaling, and control evidence are reproducible.
- No real clipboard content, credentials, global hooks, persistent history, telemetry, or private media enters the repository.
- Production integration remains blocked until the user approves Gate 0C.
