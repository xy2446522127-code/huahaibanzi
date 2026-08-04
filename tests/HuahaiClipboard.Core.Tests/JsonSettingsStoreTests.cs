using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    [TestMethod]
    public async Task SettingsPersistAcrossInstancesIncludingInputOptions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var expected = ShellSettings.Default with
            {
                Appearance = ShellSettings.Default.Appearance with { ThemeId = "cobalt-blue" },
                Input = new InputSettings(false, true, ["secret.exe"])
            };
            var first = new JsonSettingsStore(path);

            await first.SaveAsync(expected, CancellationToken.None);

            var second = new JsonSettingsStore(path);
            var actual = await second.LoadAsync(CancellationToken.None);
            Assert.AreEqual("cobalt-blue", actual.Appearance.ThemeId);
            Assert.IsFalse(actual.Input.RightDoubleClickEnabled);
            Assert.IsTrue(actual.Input.HotkeyEnabled);
            CollectionAssert.AreEqual(new[] { "secret.exe" }, actual.Input.ExcludedApplications);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
