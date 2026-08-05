# 花海剪贴板开源原生 UI 样机 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task. If the user explicitly requests delegated execution, assign only the bounded tasks below and retain one Git writer. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不修改当前正式 WinUI/WebView2 客户端和安装包的前提下，建立共享 UI 规范与隔离 WPF 样机，实测后台内存、热唤出、滚动帧率和视觉一致性是否达到迁移门槛。

**Architecture:** 当前 `product-shell.html` 与视觉源 1.0.4 保持唯一视觉母版。新增 `ui/huahai-ui-spec.json`，由 Node 生成 Web CSS 变量和 WPF `ResourceDictionary`；隔离的 .NET 8 WPF 样机引用现有 Core，但只使用固定模拟数据和独立本机设置，不接管正式剪贴板、托盘、开机启动或安装入口。样机通过全部性能与视觉门槛并经用户确认后，再为真实迁移编写第二份实施计划。

**Tech Stack:** .NET 8、WPF（MIT）、C# 12、MSTest 3.9.3、Node.js 内置测试运行器、PowerShell、Windows DWM、现有 `HuahaiClipboard.Core`。

## Global Constraints

- 产品规模保持 `micro`；本计划只交付隔离技术样机，不替换正式客户端。
- Windows 最低版本为 Windows 10 19041，目标平台固定 `x64`，同时验证 Windows 10 与 Windows 11。
- 不引入 Sciter、Ultralight、Servo、Blitz、WebView2、Chromium、Electron、CEF 或新的生产 NuGet 包。
- 当前 `src/HuahaiClipboard.App/Assets/Web/product-shell.html` 和视觉源 1.0.4 是唯一视觉母版，不允许重新设计。
- 保留狐狸图标、艺术字、五套主题、透明玻璃边缘、背景花瓣、A + E 点击动效、现有字体比例和设置结构。
- 样机只读取固定模拟数据；不得读取、修改或清理用户真实剪贴板历史、设置、图片缓存和窗口位置。
- 后台 60 秒后的 Private Working Set 允许区间为 40–70MB；无浏览器或第三方渲染子进程。
- 热唤出 30 次的 P95 小于或等于 50ms；1,000 条记录滚动目标不低于 55FPS。
- WPF 样机未通过门槛时停止，不以裁剪工作集、隐藏统计、降低文字质量或删除已批准动效伪造结果。
- 当前安装包和正式入口只有在后续真实迁移计划、完整验证与用户批准后才能改变。

---

## File Map

```text
ui/
  huahai-ui-spec.json                         共享、版本化 UI 合同
  generated/huahai-ui-tokens.css             生成的 Web 变量
  generated/HuahaiUiTokens.xaml               生成的 WPF 资源
tools/
  generate-huahai-ui-spec.mjs                 确定性校验与生成器
tests/
  UiSpecGeneratorTests.cjs                    生成器行为测试
  HuahaiClipboard.NativeUiSpike.Tests/
    HuahaiClipboard.NativeUiSpike.Tests.csproj
    NativeUiSpikeViewModelTests.cs
    PanelWindowStateControllerTests.cs
    UiResourceLoadingTests.cs
  HuahaiClipboard.NativeUiSpike.Smoke/
    NativeUiSpikePerformance.ps1              内存、子进程和唤出延迟
    NativeUiSpikeWindowSmoke.ps1               显示、置顶、隐藏与存活
    CaptureNativeUiSpike.ps1                   固定条件窗口截图
    CompareUiEvidence.ps1                      图像尺寸和像素差异报告
experiments/HuahaiClipboard.NativeUiSpike/
  HuahaiClipboard.NativeUiSpike.csproj         隔离 WPF 样机工程
  App.xaml
  App.xaml.cs
  Models/SpikeClipboardItem.cs
  Presentation/NativeUiSpikeViewModel.cs
  Presentation/PanelWindowStateController.cs
  Presentation/Windows/MainWindow.xaml
  Presentation/Windows/MainWindow.xaml.cs
  Presentation/Views/PanelView.xaml
  Presentation/Views/PanelView.xaml.cs
  Presentation/Views/SettingsView.xaml
  Presentation/Views/SettingsView.xaml.cs
  Presentation/Controls/GlassSurface.xaml
  Presentation/Controls/ClipboardRecordView.xaml
  Presentation/Styles/NativeUiStyles.xaml
  Services/SingleInstanceActivationService.cs
  Services/WindowCompositionService.cs
  Diagnostics/FrameTimingProbe.cs
  Assets/fox-icon.ico                         链接现有狐狸图标，不复制新设计
docs/product/native-ui-spike-report.md         最终证据和门槛结论
```

### Task 1: 共享 UI 规范与确定性生成器

**Files:**
- Create: `ui/huahai-ui-spec.json`
- Create: `tools/generate-huahai-ui-spec.mjs`
- Create: `ui/generated/huahai-ui-tokens.css`
- Create: `ui/generated/HuahaiUiTokens.xaml`
- Create: `tests/UiSpecGeneratorTests.cjs`

**Interfaces:**
- Consumes: approved visual source version `1.0.4`; theme IDs `rose-purple`, `cobalt-blue`, `emerald-cyan`, `amber-orange`, `aurora-cyan-purple`.
- Produces: `validateSpec(spec): string[]`, `renderCss(spec): string`, `renderWpf(spec): string`, and the generated CSS/XAML files used by later tasks.

- [ ] **Step 1: Write failing generator tests**

Create `tests/UiSpecGeneratorTests.cjs` with literal expectations independent of the generator:

```javascript
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const { pathToFileURL } = require('node:url');

const spec = JSON.parse(fs.readFileSync('ui/huahai-ui-spec.json', 'utf8'));
let validateSpec;
let renderCss;
let renderWpf;

test.before(async () => {
  ({ validateSpec, renderCss, renderWpf } = await import(
    pathToFileURL(path.resolve('tools/generate-huahai-ui-spec.mjs')).href
  ));
});

test('approved UI spec has the locked surfaces and five themes', () => {
  assert.deepEqual(validateSpec(spec), []);
  assert.equal(spec.schemaVersion, 1);
  assert.equal(spec.visualSourceVersion, '1.0.4');
  assert.deepEqual(spec.panel, { width: 430, height: 680, cornerRadius: 29 });
  assert.deepEqual(spec.settings, { width: 820, height: 650 });
  assert.deepEqual(spec.themes.map(theme => theme.id), [
    'rose-purple',
    'cobalt-blue',
    'emerald-cyan',
    'amber-orange',
    'aurora-cyan-purple',
  ]);
});

test('generator emits stable Web and WPF keys', () => {
  const css = renderCss(spec);
  const xaml = renderWpf(spec);
  assert.match(css, /--huahai-panel-width:430px/);
  assert.match(css, /--huahai-click-duration:620ms/);
  assert.match(xaml, /x:Key="HuahaiPanelWidth">430<\/sys:Double>/);
  assert.match(xaml, /x:Key="HuahaiPanelCornerRadius">29<\/CornerRadius>/);
  assert.match(xaml, /x:Key="HuahaiThemeCount">5<\/sys:Int32>/);
});

test('generator rejects a missing approved theme', () => {
  const invalid = structuredClone(spec);
  invalid.themes.pop();
  assert.deepEqual(validateSpec(invalid), ['themes must contain exactly 5 entries']);
});
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
node --test tests\UiSpecGeneratorTests.cjs
```

Expected: FAIL because `ui/huahai-ui-spec.json` and `tools/generate-huahai-ui-spec.mjs` do not exist.

- [ ] **Step 3: Create the locked UI specification**

Create `ui/huahai-ui-spec.json` with this top-level contract and the exact ARGB values already present in `ThemeCatalog`:

```json
{
  "schemaVersion": 1,
  "visualSourceVersion": "1.0.4",
  "panel": { "width": 430, "height": 680, "cornerRadius": 29 },
  "settings": { "width": 820, "height": 650 },
  "typography": {
    "family": "Segoe UI Variable Text",
    "recordPrimary": 14,
    "recordMetadata": 11,
    "settingsLabel": 12,
    "settingsDescription": 10
  },
  "motion": {
    "clickDurationMs": 620,
    "reducedClickDurationMs": 120,
    "petalCountLow": 5,
    "specularProximityPx": 10
  },
  "themes": [
    { "id": "rose-purple", "accent": "#FFD786BB", "reflection": "#FF8F5BAA", "glassTop": "#8FA05697", "glassBottom": "#E026112D", "contentLens": "#6B230E29", "focus": "#A3D786BB", "text": "#FFFFF5FC", "muted": "#FFC1B2C0" },
    { "id": "cobalt-blue", "accent": "#FF72AEF0", "reflection": "#FF365FA8", "glassTop": "#94436AA9", "glassBottom": "#E6111D3A", "contentLens": "#6B111D3A", "focus": "#A372AEF0", "text": "#FFFFF5FC", "muted": "#FFC1B2C0" },
    { "id": "emerald-cyan", "accent": "#FF6CCBAD", "reflection": "#FF287F77", "glassTop": "#94318474", "glassBottom": "#E60D312F", "contentLens": "#6B0D312F", "focus": "#A36CCBAD", "text": "#FFFFF5FC", "muted": "#FFC1B2C0" },
    { "id": "amber-orange", "accent": "#FFE5AD70", "reflection": "#FFA36B56", "glassTop": "#949D654B", "glassBottom": "#E63A1F1C", "contentLens": "#6B3A1F1C", "focus": "#A3E5AD70", "text": "#FFFFF5FC", "muted": "#FFC1B2C0" },
    { "id": "aurora-cyan-purple", "accent": "#FF78D7DF", "reflection": "#FF8E72CF", "glassTop": "#94397B91", "glassBottom": "#E61A2246", "contentLens": "#6B1A2246", "focus": "#A378D7DF", "text": "#FFFFF5FC", "muted": "#FFC1B2C0" }
  ]
}
```

- [ ] **Step 4: Implement validation and deterministic generation**

Implement `tools/generate-huahai-ui-spec.mjs` so it exports the three functions, sorts output by fixed key order, writes UTF-8 without timestamps, exits non-zero on validation errors, and only rewrites generated files when content changes. The command-line entry point must be:

```javascript
if (import.meta.url === `file://${process.argv[1].replaceAll('\\', '/')}`) {
  const spec = JSON.parse(fs.readFileSync('ui/huahai-ui-spec.json', 'utf8'));
  const errors = validateSpec(spec);
  if (errors.length) {
    process.stderr.write(`${errors.join('\n')}\n`);
    process.exitCode = 1;
  } else {
    writeIfChanged('ui/generated/huahai-ui-tokens.css', renderCss(spec));
    writeIfChanged('ui/generated/HuahaiUiTokens.xaml', renderWpf(spec));
  }
}
```

The generated XAML root must be a `ResourceDictionary` with `sys:Double`, `sys:Int32`, `CornerRadius`, `Color`, and `SolidColorBrush` entries. Do not emit application behavior into generated files.

- [ ] **Step 5: Generate outputs and verify GREEN**

Run:

```powershell
node tools\generate-huahai-ui-spec.mjs
node --test tests\UiSpecGeneratorTests.cjs
$before = Get-ChildItem ui\generated -File | Get-FileHash -Algorithm SHA256
node tools\generate-huahai-ui-spec.mjs
$after = Get-ChildItem ui\generated -File | Get-FileHash -Algorithm SHA256
if (Compare-Object $before $after -Property Path,Hash) { throw 'generator output is not deterministic' }
```

Expected: 3 tests pass; a second generator run produces no diff.

- [ ] **Step 6: Commit the shared contract**

```powershell
git add -- ui/huahai-ui-spec.json ui/generated tools/generate-huahai-ui-spec.mjs tests/UiSpecGeneratorTests.cjs
git commit -m "feat: add shared native UI specification"
```

### Task 2: Isolated WPF host and real resource loading

**Files:**
- Create: `experiments/HuahaiClipboard.NativeUiSpike/HuahaiClipboard.NativeUiSpike.csproj`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/App.xaml`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/App.xaml.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Windows/MainWindow.xaml`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Windows/MainWindow.xaml.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Styles/NativeUiStyles.xaml`
- Create: `tests/HuahaiClipboard.NativeUiSpike.Tests/HuahaiClipboard.NativeUiSpike.Tests.csproj`
- Create: `tests/HuahaiClipboard.NativeUiSpike.Tests/UiResourceLoadingTests.cs`

**Interfaces:**
- Consumes: `ui/generated/HuahaiUiTokens.xaml` and `HuahaiClipboard.Core`.
- Produces: a buildable x64 WPF process with a 430×680 transparent, frameless window and loadable generated resources.

- [ ] **Step 1: Write a failing STA resource-loading test**

Create the Windows-targeted MSTest project using the repository package versions and reference the spike project. Add:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class UiResourceLoadingTests
{
    [STATestMethod]
    public void GeneratedDictionary_LoadsLockedPanelGeometry()
    {
        var dictionary = new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/HuahaiClipboard.NativeUiSpike;component/Generated/HuahaiUiTokens.xaml")
        };

        Assert.AreEqual(430d, dictionary["HuahaiPanelWidth"]);
        Assert.AreEqual(680d, dictionary["HuahaiPanelHeight"]);
        Assert.AreEqual(new CornerRadius(29), dictionary["HuahaiPanelCornerRadius"]);
        Assert.AreEqual(5, dictionary["HuahaiThemeCount"]);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

```powershell
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj --no-restore --filter FullyQualifiedName~UiResourceLoadingTests
```

Expected: FAIL because the WPF project and pack resource do not exist.

- [ ] **Step 3: Create the isolated WPF project**

Use these project properties:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <PlatformTarget>x64</PlatformTarget>
    <RootNamespace>HuahaiClipboard.NativeUiSpike</RootNamespace>
    <ApplicationIcon>Assets\fox-icon.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\HuahaiClipboard.Core\HuahaiClipboard.Core.csproj" />
    <Resource Include="..\..\ui\generated\HuahaiUiTokens.xaml" Link="Generated\HuahaiUiTokens.xaml" />
    <Resource Include="..\..\src\HuahaiClipboard.App\Assets\Brand\fox-icon.ico" Link="Assets\fox-icon.ico" />
  </ItemGroup>
</Project>
```

Do not add the experimental project to `HuahaiClipboard.sln`; build it explicitly so the current release graph remains unchanged.

- [ ] **Step 4: Create the frameless host**

`MainWindow.xaml` must set `WindowStyle="None"`, `ResizeMode="NoResize"`, `AllowsTransparency="False"`, `Background="Transparent"`, `ShowInTaskbar="False"`, `Width="430"`, and `Height="680"`. `AllowsTransparency` stays off so WPF can keep hardware composition; Task 4 removes the native frame and applies the rounded DWM-backed surface. Merge `Generated/HuahaiUiTokens.xaml` and `Presentation/Styles/NativeUiStyles.xaml` in `App.xaml`.

The initial content must be:

```xml
<Border x:Name="GlassRoot"
        CornerRadius="{DynamicResource HuahaiPanelCornerRadius}"
        Background="{DynamicResource HuahaiGlassBrush}"
        ClipToBounds="True">
    <Grid>
        <ContentControl x:Name="PanelHost" />
        <ContentControl x:Name="SettingsHost" Visibility="Collapsed" />
    </Grid>
</Border>
```

- [ ] **Step 5: Verify resource loading and build**

```powershell
dotnet restore experiments\HuahaiClipboard.NativeUiSpike\HuahaiClipboard.NativeUiSpike.csproj
dotnet restore tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj --no-restore --filter FullyQualifiedName~UiResourceLoadingTests
dotnet build experiments\HuahaiClipboard.NativeUiSpike\HuahaiClipboard.NativeUiSpike.csproj -c Release --no-restore
```

Expected: 1 test passes and Release build exits 0 without warnings.

- [ ] **Step 6: Commit the WPF host**

```powershell
git add -- experiments/HuahaiClipboard.NativeUiSpike tests/HuahaiClipboard.NativeUiSpike.Tests
git commit -m "feat: scaffold isolated native UI spike"
```

### Task 3: Native panel, settings, and 1,000-record interaction model

**Files:**
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Models/SpikeClipboardItem.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/NativeUiSpikeViewModel.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Views/PanelView.xaml`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Views/PanelView.xaml.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Views/SettingsView.xaml`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Views/SettingsView.xaml.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Controls/GlassSurface.xaml`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Controls/ClipboardRecordView.xaml`
- Create: `tests/HuahaiClipboard.NativeUiSpike.Tests/NativeUiSpikeViewModelTests.cs`

**Interfaces:**
- Consumes: generated token resources and fixed theme IDs.
- Produces: `NativeUiSpikeViewModel.CreateFixture(int count)`, `VisibleItems`, `TogglePinned(Guid)`, `ToggleFavorite(Guid)`, `Delete(Guid)`, `SelectFilter(ClipboardFilter)`, `SetTheme(string)`, and `OpenSettings(bool)`.

- [ ] **Step 1: Write failing interaction tests**

```csharp
[TestClass]
public sealed class NativeUiSpikeViewModelTests
{
    [TestMethod]
    public void Fixture_ContainsExactlyOneThousandStableRecords()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(1000);
        Assert.AreEqual(1000, model.AllItems.Count);
        Assert.AreEqual("fixture-0001", model.AllItems[0].StableId);
        Assert.AreEqual("fixture-1000", model.AllItems[^1].StableId);
    }

    [TestMethod]
    public void PinFavoriteDeleteAndFilter_ChangeObservableState()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(12);
        var target = model.AllItems[3];
        model.TogglePinned(target.Id);
        model.ToggleFavorite(target.Id);
        model.SelectFilter(ClipboardFilter.Favorites);
        Assert.IsTrue(target.IsPinned);
        Assert.IsTrue(target.IsFavorite);
        Assert.AreEqual(1, model.VisibleItems.Count);
        model.Delete(target.Id);
        Assert.AreEqual(0, model.VisibleItems.Count);
        Assert.AreEqual(11, model.AllItems.Count);
    }

    [TestMethod]
    public void UnknownTheme_IsRejectedWithoutChangingCurrentTheme()
    {
        var model = NativeUiSpikeViewModel.CreateFixture(1);
        Assert.IsFalse(model.SetTheme("not-a-theme"));
        Assert.AreEqual("rose-purple", model.ThemeId);
    }
}
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj --no-restore --filter FullyQualifiedName~NativeUiSpikeViewModelTests
```

Expected: FAIL because the model and methods are missing.

- [ ] **Step 3: Implement the deterministic mock model**

`SpikeClipboardItem` must expose `Id`, `StableId`, `Kind`, `Title`, `Metadata`, `IsPinned`, and `IsFavorite` with property-change notification. `CreateFixture` must cycle through text, link, image, and file kinds; use fixed timestamps and filenames; never read the real clipboard or user storage.

`NativeUiSpikeViewModel` must keep an `ObservableCollection<SpikeClipboardItem>` and a materialized read-only `VisibleItems` list. Every mutation recalculates the visible list and raises `PropertyChanged`. Theme IDs are validated against the fixed five-item set from the shared spec.

- [ ] **Step 4: Build the panel with real virtualization**

`PanelView.xaml` must use a `ListBox` with:

```xml
<ListBox ItemsSource="{Binding VisibleItems}"
         ScrollViewer.CanContentScroll="True"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling">
    <ListBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </ListBox.ItemsPanel>
</ListBox>
```

Add the approved header, fox icon, artistic wordmark, search box, minimize and settings buttons, filter row, history count, four kind icons, pin/favorite/delete actions, and fixed footer. Do not add native borders outside `GlassRoot`.

- [ ] **Step 5: Build the complete settings surface**

`SettingsView.xaml` must preserve the current two-column navigation and provide these six reachable pages: appearance, motion, input, storage, system, and about. Every control performs a mock state change and shows a local toast. Controls that require real Windows integration display `样机：正式版接入后生效` and remain non-destructive.

The five theme buttons, opacity, panel scale, petal toggle, reduced motion, shortcut capture simulation, application exclusions, retention options, startup toggle, background toggle, data folder preview, ordinary clear, and clear-all confirmation must be reachable with keyboard focus.

- [ ] **Step 6: Verify tests, build, and 1,000-item startup**

```powershell
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj --no-restore
dotnet build experiments\HuahaiClipboard.NativeUiSpike\HuahaiClipboard.NativeUiSpike.csproj -c Release --no-restore
```

Expected: all spike tests pass and the Release process opens with exactly 1,000 mock items.

- [ ] **Step 7: Commit the interactive surfaces**

```powershell
git add -- experiments/HuahaiClipboard.NativeUiSpike tests/HuahaiClipboard.NativeUiSpike.Tests
git commit -m "feat: build interactive native clipboard surfaces"
```

### Task 4: Window lifecycle, instant activation, glass, and motion

**Files:**
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/PanelWindowStateController.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Services/SingleInstanceActivationService.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Services/WindowCompositionService.cs`
- Create: `experiments/HuahaiClipboard.NativeUiSpike/Diagnostics/FrameTimingProbe.cs`
- Modify: `experiments/HuahaiClipboard.NativeUiSpike/App.xaml.cs`
- Modify: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Windows/MainWindow.xaml.cs`
- Modify: `experiments/HuahaiClipboard.NativeUiSpike/Presentation/Styles/NativeUiStyles.xaml`
- Test: `tests/HuahaiClipboard.NativeUiSpike.Tests/PanelWindowStateControllerTests.cs`
- Test: `tests/HuahaiClipboard.NativeUiSpike.Smoke/NativeUiSpikeWindowSmoke.ps1`

**Interfaces:**
- Consumes: a pre-created `MainWindow` and shared motion tokens.
- Produces: `PanelWindowStateController.ShowAt(Point cursor)`, `Hide()`, `OpenSettings()`, `CloseSettings()`, plus `SingleInstanceActivationService.Activated`.

- [ ] **Step 1: Write failing window-state tests**

Use a real controller with an in-memory host and assert exact call order. Also create `NativeUiSpikeWindowSmoke.ps1` before implementing runtime behavior; it must launch `--background`, verify the first process remains alive with a hidden window, activate it, assert visible plus `WS_EX_TOPMOST`, send `WM_CLOSE`, then assert hidden plus non-topmost while the process remains alive. The script owns and terminates only the process it starts and emits JSON fields `SummonedTopmost`, `SummonedVisible`, `HiddenTopmost`, `HiddenVisible`, and `ProcessAlive`.

```csharp
[TestMethod]
public void ShowAt_UpdatesStateBeforeMakingWindowVisible()
{
    var host = new RecordingPanelWindowHost();
    var controller = new PanelWindowStateController(host);
    controller.ShowAt(new Point(1200, 500));
    CollectionAssert.AreEqual(
        new[] { "refresh", "move:1200,500", "topmost:on", "show", "focus" },
        host.Actions);
}

[TestMethod]
public void Hide_RemovesTopmostBeforeHiding()
{
    var host = new RecordingPanelWindowHost();
    var controller = new PanelWindowStateController(host);
    controller.Hide();
    CollectionAssert.AreEqual(new[] { "topmost:off", "hide" }, host.Actions);
}
```

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj --no-restore --filter FullyQualifiedName~PanelWindowStateControllerTests
```

Expected: FAIL because the controller and host interface do not exist.

After the first buildable RED state exists, run the smoke script as well. Expected: FAIL with `Summoning must show a topmost panel` until the lifecycle implementation below is complete.

- [ ] **Step 3: Implement lifecycle and single-instance activation**

Use one named mutex and one named `EventWaitHandle`:

```csharp
private const string MutexName = "Local\\HuahaiClipboard.NativeUiSpike.Mutex";
private const string ActivationName = "Local\\HuahaiClipboard.NativeUiSpike.Activate";
```

The first process pre-creates the hidden window and waits for the activation event on a background thread. A second process sets the event and exits. The event handler posts `ShowAt(GetCursorPosition())` to the WPF dispatcher. `--background` starts hidden; a normal first launch shows the window.

- [ ] **Step 4: Implement native window composition**

`WindowCompositionService` must remove native frame styles, apply a 29px rounded region at the current DPI, use DWM backdrop when available, fall back to the approved opaque lens on unsupported Windows 10 systems, and expose `SetTopmost(bool)`. Do not use `AllowsTransparency` effects that force software rendering if the DWM-backed path is available; record the selected mode for diagnostics.

- [ ] **Step 5: Implement approved motion without layout animation**

Use WPF transforms and opacity only:

- A + E record feedback: `TranslateTransform.Y`, `ScaleTransform`, theme-colored ripple, 620ms; reduced mode 120ms.
- Button specular response: pointer distance 10 logical pixels, update at most once per render frame.
- Low petals: five 7px-or-smaller background-layer shapes; pause while hidden or reduced motion.
- Liquid reflection: one clipped gradient transform; pause while hidden.

`FrameTimingProbe` subscribes to `CompositionTarget.Rendering` only when `--diagnostics` is supplied and writes count, mean interval, P95 interval, and derived FPS as one JSON line on exit.

- [ ] **Step 6: Verify unit tests and build**

```powershell
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj --no-restore
dotnet build experiments\HuahaiClipboard.NativeUiSpike\HuahaiClipboard.NativeUiSpike.csproj -c Release --no-restore
```

Expected: all tests pass; no browser engine package appears in `dotnet list ... package --include-transitive`.

- [ ] **Step 7: Commit lifecycle and motion**

```powershell
git add -- experiments/HuahaiClipboard.NativeUiSpike tests/HuahaiClipboard.NativeUiSpike.Tests
git commit -m "feat: add instant native panel lifecycle and motion"
```

### Task 5: Deterministic Windows performance and visual gates

**Files:**
- Create: `tests/HuahaiClipboard.NativeUiSpike.Smoke/NativeUiSpikePerformance.ps1`
- Modify: `tests/HuahaiClipboard.NativeUiSpike.Smoke/NativeUiSpikeWindowSmoke.ps1`
- Create: `tests/HuahaiClipboard.NativeUiSpike.Smoke/CaptureNativeUiSpike.ps1`
- Create: `tests/HuahaiClipboard.NativeUiSpike.Smoke/CompareUiEvidence.ps1`
- Create: `docs/product/native-ui-spike-report.md`

**Interfaces:**
- Consumes: x64 Release spike executable, approved Web baseline, fixed 1,000-record fixture.
- Produces: machine-readable JSON evidence for memory, child processes, 30 summon samples, visibility/topmost, frame timing, screenshot dimensions, differing-pixel ratio, and a pass/fail report.

- [ ] **Step 1: Complete and run the window smoke**

Finish the Task 4 smoke adapter and verify it performs these checks:

1. launch the spike with `--background`;
2. verify the process stays alive and the window is hidden;
3. launch a second instance and wait for visibility;
4. assert `WS_EX_TOPMOST` is set;
5. send `WM_CLOSE` and assert the process stays alive, the window hides, and topmost clears;
6. terminate only the process started by the script;
7. emit JSON with `SummonedTopmost`, `SummonedVisible`, `HiddenTopmost`, `HiddenVisible`, and `ProcessAlive`.

- [ ] **Step 2: Run the completed lifecycle smoke**

```powershell
& tests\HuahaiClipboard.NativeUiSpike.Smoke\NativeUiSpikeWindowSmoke.ps1 `
  -ExePath experiments\HuahaiClipboard.NativeUiSpike\bin\Release\net8.0-windows10.0.19041.0\HuahaiClipboard.NativeUiSpike.exe
```

Expected: PASS with all five JSON fields true except `HiddenTopmost` and `HiddenVisible`, which must both be false. Do not mutate working production code merely to manufacture a RED result; the pre-implementation RED belongs to Task 4.

- [ ] **Step 3: Implement the performance probe**

`NativeUiSpikePerformance.ps1` must measure Private Working Set, working set, CPU, handle count, child process names, and summon latency using `Stopwatch.GetTimestamp()`. Required sampling:

- launch `--background` and wait 60 seconds;
- take three memory samples 10 seconds apart;
- fail if median Private Working Set is outside 40–70MB;
- fail if any descendant process name contains `webview`, `edge`, `chrome`, `sciter`, `servo`, or `blitz`;
- open `Local\HuahaiClipboard.NativeUiSpike.Activate` from PowerShell and set the named event directly for 30 in-process hot-activation samples, measuring from `Set()` until the window is visible and focused; calculate P50/P95 without including a second .NET process startup;
- fail if P95 exceeds 50ms;
- always stop owned processes in `finally`.

- [ ] **Step 4: Implement screenshot capture and comparison**

`CaptureNativeUiSpike.ps1` must capture the window client area at 430×680 and 820×650 for the rose theme, plus 430×680 for the other four themes. Use `PrintWindow` with `PW_RENDERFULLCONTENT` and save PNGs under ignored `.codex/artifacts/ui-qa/native-spike/`.

`CompareUiEvidence.ps1` must use `System.Drawing.Bitmap`, reject mismatched dimensions, compare RGB values while ignoring fully transparent pixels, and emit:

```json
{
  "width": 430,
  "height": 680,
  "differentPixelRatio": 0.0,
  "meanAbsoluteChannelDifference": 0.0
}
```

The script records numeric evidence but does not automatically approve typography anti-aliasing differences. Save the approved Web image, native image, and difference image only.

- [ ] **Step 5: Run the complete spike gate**

```powershell
node --test tests\UiSpecGeneratorTests.cjs
dotnet test tests\HuahaiClipboard.NativeUiSpike.Tests\HuahaiClipboard.NativeUiSpike.Tests.csproj --no-restore
dotnet build experiments\HuahaiClipboard.NativeUiSpike\HuahaiClipboard.NativeUiSpike.csproj -c Release --no-restore
& tests\HuahaiClipboard.NativeUiSpike.Smoke\NativeUiSpikeWindowSmoke.ps1 -ExePath experiments\HuahaiClipboard.NativeUiSpike\bin\Release\net8.0-windows10.0.19041.0\HuahaiClipboard.NativeUiSpike.exe
& tests\HuahaiClipboard.NativeUiSpike.Smoke\NativeUiSpikePerformance.ps1 -ExePath experiments\HuahaiClipboard.NativeUiSpike\bin\Release\net8.0-windows10.0.19041.0\HuahaiClipboard.NativeUiSpike.exe
```

Run the visual capture at 100%, 150%, and 200% DPI. Run the interaction pass with 1,000 records, all five themes, reduced motion on/off, petals on/off, panel/settings transitions, keyboard focus, and mouse wheel scrolling.

- [ ] **Step 6: Write the evidence report**

`docs/product/native-ui-spike-report.md` must contain:

- exact commit and executable path;
- OS build, DPI, CPU, GPU, RAM, .NET runtime;
- three background memory samples and median;
- descendant process list;
- 30 summon samples with P50/P95;
- frame timing result for list scroll and combined motion;
- links to the bounded visual evidence;
- every failed criterion without rounding it into a pass;
- decision `pass-for-user-review` or `stop-native-migration`.

- [ ] **Step 7: Run the delivery gate and commit evidence adapters**

```powershell
python "F:\Codex Data\skills\app-product-delivery\scripts\delivery_gate.py" `
  "F:\Users\DXY\Documents\桌面粘贴悬浮面板" `
  --gate checkpoint `
  --owned-path experiments/HuahaiClipboard.NativeUiSpike `
  --owned-path tests/HuahaiClipboard.NativeUiSpike.Smoke `
  --owned-path docs/product/native-ui-spike-report.md

git add -- experiments/HuahaiClipboard.NativeUiSpike tests/HuahaiClipboard.NativeUiSpike.Smoke docs/product/native-ui-spike-report.md
git commit -m "test: verify native UI feasibility gates"
```

### Task 6: User experience gate and next-plan boundary

**Files:**
- Modify: `docs/product/native-ui-spike-report.md`
- Modify only after approval: `.codex/app-product-delivery-visual-source.json`

**Interfaces:**
- Consumes: verified performance report and native interactive spike.
- Produces: one explicit outcome: `approved-for-production-migration` or `rejected-with-current-release-preserved`.

- [ ] **Step 1: Present the real native spike without packaging it**

Launch the verified x64 Release spike, provide the exact executable folder, and ask the user to experience main panel, settings, five themes, scaling, mouse wheel, pin/favorite/delete mocks, petals, liquid reflection, button specular effect, and hide/summon behavior.

- [ ] **Step 2: Record the user decision**

If approved, append this exact field to the report:

```yaml
user_decision: approved-for-production-migration
```

If rejected, append:

```yaml
user_decision: rejected-with-current-release-preserved
```

Do not infer approval from a performance pass.

- [ ] **Step 3: Close the spike stage**

On approval, update the visual source with a child baseline referencing visual source 1.0.4, the native evidence, scope `renderer-migration-spike`, and the approval checkpoint. Then write a new production-migration implementation plan covering real adapters, data compatibility, shortcut/tray/startup, installer, Windows 10/11 matrix, rollback, and release.

On rejection or failed performance, stop. Preserve the current executable, installer, user data, and Git history; record the failed criteria in the backlog. Do not begin Direct2D work without a new user-approved design change.

- [ ] **Step 4: Commit only the decision record**

```powershell
git add -- docs/product/native-ui-spike-report.md .codex/app-product-delivery-visual-source.json
git commit -m "docs: record native UI spike decision"
```

The `.codex` path must be omitted from `git add` when the gate marks it as pre-existing overlapping work; never force the checkpoint.
