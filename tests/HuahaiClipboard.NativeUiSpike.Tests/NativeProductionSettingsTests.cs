using HuahaiClipboard.Core.Settings;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class NativeProductionSettingsTests
{
    [TestMethod]
    public void NewSettingsFieldsHaveBackwardCompatibleDefaults()
    {
        var appearance = new AppearanceSettings("rose-purple", 0.88, 32, 0.72, false);
        var behavior = new BehaviorSettings(true, 7);

        Assert.AreEqual(1d, appearance.PanelScale, 0.001);
        Assert.IsTrue(behavior.CheckUpdatesOnStartup);
    }

    [TestMethod]
    public async Task ProductionModelPersistsAppearanceMotionInputAndBehavior()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var history = new HuahaiClipboard.Core.Services.JsonClipboardHistorySource(
                Path.Combine(directory, "history.dat"),
                new PassthroughProtector());
            var settingsStore = new HuahaiClipboard.Core.Services.JsonSettingsStore(Path.Combine(directory, "settings.json"));
            var actions = new HuahaiClipboard.Core.Services.ClipboardPanelActionSink(history, new NoopClipboard());
            var model = HuahaiClipboard.NativeUiSpike.Presentation.NativeUiSpikeViewModel.CreateProduction(
                history,
                actions,
                settingsStore);
            await model.LoadAsync();

            await model.UpdateAppearanceAsync("cobalt-blue", 0.74, 1.25);
            await model.UpdateMotionAsync(PetalLevel.Off, true, 480);
            await model.UpdateInputAsync(false, true, ["secret.exe"], "Ctrl + Alt + H");
            await model.UpdateBehaviorAsync(false, 3, false);

            var saved = await settingsStore.LoadAsync(CancellationToken.None);
            Assert.AreEqual("cobalt-blue", saved.Appearance.ThemeId);
            Assert.AreEqual(0.74, saved.Appearance.Opacity, 0.001);
            Assert.AreEqual(1.25, saved.Appearance.PanelScale, 0.001);
            Assert.AreEqual(PetalLevel.Off, saved.Motion.PetalLevel);
            Assert.IsTrue(saved.Motion.ReduceMotion);
            Assert.AreEqual(480, saved.Motion.ClickDurationMs);
            Assert.IsFalse(saved.Input.RightDoubleClickEnabled);
            Assert.AreEqual("Ctrl + Alt + H", saved.Input.CustomShortcut);
            CollectionAssert.AreEqual(new[] { "secret.exe" }, saved.Input.ExcludedApplications);
            Assert.IsFalse(saved.Behavior.BackgroundEnabled);
            Assert.AreEqual(3, saved.Behavior.AutoCleanupDays);
            Assert.IsFalse(saved.Behavior.CheckUpdatesOnStartup);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class PassthroughProtector : HuahaiClipboard.Core.Contracts.ITextProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }

    private sealed class NoopClipboard : HuahaiClipboard.Core.Contracts.IClipboardPlatform
    {
        public Task WriteAsync(HuahaiClipboard.Core.Models.ClipboardRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> PasteAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
