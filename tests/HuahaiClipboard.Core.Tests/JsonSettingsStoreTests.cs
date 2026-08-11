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
                Input = new InputSettings(false, true, ["secret.exe"]),
                Behavior = new BehaviorSettings(
                    false,
                    30,
                    true,
                    "1.1.7",
                    new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
                    HideOnOutsideClick: false)
            };
            var first = new JsonSettingsStore(path);

            await first.SaveAsync(expected, CancellationToken.None);

            var second = new JsonSettingsStore(path);
            var actual = await second.LoadAsync(CancellationToken.None);
            Assert.AreEqual("cobalt-blue", actual.Appearance.ThemeId);
            Assert.IsFalse(actual.Input.RightDoubleClickEnabled);
            Assert.IsTrue(actual.Input.HotkeyEnabled);
            CollectionAssert.AreEqual(new[] { "secret.exe" }, actual.Input.ExcludedApplications);
            Assert.IsFalse(actual.Behavior.BackgroundEnabled);
            Assert.AreEqual(30, actual.Behavior.AutoCleanupDays);
            Assert.AreEqual("1.1.7", actual.Behavior.SnoozedUpdateVersion);
            Assert.IsFalse(actual.Behavior.HideOnOutsideClick);
            Assert.AreEqual(
                new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero),
                actual.Behavior.UpdateSnoozeUntil);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Settings_MissingOutsideHidePreferenceDefaultsToEnabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-settings-outside-hide-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            var json = System.Text.Json.Nodes.JsonNode.Parse(
                System.Text.Json.JsonSerializer.Serialize(ShellSettings.Default))!.AsObject();
            json["Behavior"]!.AsObject().Remove("HideOnOutsideClick");
            await File.WriteAllTextAsync(path, json.ToJsonString());

            var actual = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

            Assert.IsTrue(actual.Behavior.HideOnOutsideClick);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [DataTestMethod]
    [DataRow(-1, 7)]
    [DataRow(3, 3)]
    [DataRow(7, 7)]
    [DataRow(30, 30)]
    [DataRow(31, 7)]
    public async Task Settings_NormalizeAutoCleanupDaysToSupportedValues(int storedDays, int expectedDays)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-settings-retention-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            var settings = ShellSettings.Default with
            {
                Behavior = new BehaviorSettings(true, storedDays)
            };
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(settings));

            var actual = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);

            Assert.AreEqual(expectedDays, actual.Behavior.AutoCleanupDays);
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
