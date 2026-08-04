using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class SettingsViewModelTests
{
    [TestMethod]
    public async Task Load_ExposesStoredSettingsThemesAndPetalLevels()
    {
        var stored = ShellSettings.Default with
        {
            Appearance = ShellSettings.Default.Appearance with { ThemeId = "cobalt-blue" }
        };
        var store = new RecordingSettingsStore(stored);
        var viewModel = new SettingsViewModel(store);

        await viewModel.LoadAsync();

        Assert.AreEqual("cobalt-blue", viewModel.Draft.Appearance.ThemeId);
        Assert.AreEqual(5, viewModel.Themes.Count);
        CollectionAssert.AreEqual(
            new[] { PetalLevel.Off, PetalLevel.Low, PetalLevel.Medium, PetalLevel.High },
            viewModel.PetalLevels.ToArray());
    }

    [TestMethod]
    public async Task AppearanceUpdate_SavesAndRaisesOneFullPreviewSnapshot()
    {
        var store = new RecordingSettingsStore(ShellSettings.Default);
        var viewModel = new SettingsViewModel(store);
        var previewSnapshots = new List<ShellSettings>();
        viewModel.PreviewChanged += (_, settings) => previewSnapshots.Add(settings);
        var appearance = ShellSettings.Default.Appearance with
        {
            ThemeId = "emerald-cyan",
            Opacity = 0.92
        };

        await viewModel.UpdateAppearanceAsync(appearance);

        Assert.AreEqual(1, store.SaveCalls);
        Assert.AreEqual("emerald-cyan", store.SavedSettings!.Appearance.ThemeId);
        Assert.AreEqual(ShellSettings.Default.Motion, store.SavedSettings.Motion);
        Assert.AreEqual(1, previewSnapshots.Count);
        Assert.AreEqual(store.SavedSettings, previewSnapshots[0]);
        Assert.AreEqual("已保存", viewModel.SaveStatus);
    }

    [TestMethod]
    public async Task MotionUpdateAndAppearanceReset_PreserveTheOtherSettingsSection()
    {
        var store = new RecordingSettingsStore(ShellSettings.Default);
        var viewModel = new SettingsViewModel(store);
        var appearance = ShellSettings.Default.Appearance with { ThemeId = "amber-orange" };
        var motion = ShellSettings.Default.Motion with
        {
            PetalLevel = PetalLevel.High,
            ReduceMotion = true
        };

        await viewModel.UpdateAppearanceAsync(appearance);
        await viewModel.UpdateMotionAsync(motion);
        await viewModel.ResetAppearanceAsync();

        Assert.AreEqual(ShellSettings.Default.Appearance, viewModel.Draft.Appearance);
        Assert.AreEqual(motion, viewModel.Draft.Motion);
        Assert.AreEqual(3, store.SaveCalls);
    }

    [TestMethod]
    public async Task InputUpdate_SavesRightDoubleClickHotkeyAndExcludedApplications()
    {
        var store = new RecordingSettingsStore(ShellSettings.Default);
        var viewModel = new SettingsViewModel(store);
        var input = new InputSettings(false, true, ["game.exe", "secret.exe"]);

        await viewModel.UpdateInputAsync(input);

        Assert.AreEqual(input, viewModel.Draft.Input);
        Assert.AreEqual(input, store.SavedSettings!.Input);
    }

    private sealed class RecordingSettingsStore(ShellSettings initial) : ISettingsStore
    {
        public int SaveCalls { get; private set; }

        public ShellSettings? SavedSettings { get; private set; }

        public Task<ShellSettings> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(initial);
        }

        public Task SaveAsync(ShellSettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCalls++;
            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }
}
