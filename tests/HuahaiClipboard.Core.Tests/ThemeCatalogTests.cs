using System.Globalization;
using System.Text.RegularExpressions;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using HuahaiClipboard.Core.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ThemeCatalogTests
{
    private static readonly Regex ArgbPattern = new(
        "^#[0-9A-F]{8}$",
        RegexOptions.CultureInvariant);

    [TestMethod]
    public void Catalog_ContainsExactlyFiveNamedThemesInApprovedOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "rose-purple",
                "cobalt-blue",
                "emerald-cyan",
                "amber-orange",
                "aurora-cyan-purple"
            },
            ThemeCatalog.All.Select(theme => theme.Id).ToArray());
    }

    [TestMethod]
    public void Catalog_UsesApprovedAccentReflectionAndTextTokens()
    {
        var expected = new Dictionary<string, (string Accent, string Reflection)>
        {
            ["rose-purple"] = ("#FFD786BB", "#FF8F5BAA"),
            ["cobalt-blue"] = ("#FF72AEF0", "#FF365FA8"),
            ["emerald-cyan"] = ("#FF6CCBAD", "#FF287F77"),
            ["amber-orange"] = ("#FFE5AD70", "#FFA36B56"),
            ["aurora-cyan-purple"] = ("#FF78D7DF", "#FF8E72CF")
        };

        foreach (var theme in ThemeCatalog.All)
        {
            Assert.AreEqual(expected[theme.Id].Accent, theme.Accent, theme.Id);
            Assert.AreEqual(expected[theme.Id].Reflection, theme.Reflection, theme.Id);
            Assert.AreEqual("#FFFFF5FC", theme.TextPrimary, theme.Id);
            Assert.AreEqual("#FFC1B2C0", theme.TextSecondary, theme.Id);
        }
    }

    [TestMethod]
    public void Catalog_DefinesEveryColorAsEightDigitArgb()
    {
        foreach (var theme in ThemeCatalog.All)
        {
            var colors = new[]
            {
                theme.Accent,
                theme.Reflection,
                theme.GlassTop,
                theme.GlassBottom,
                theme.ContentLens,
                theme.FocusBorder,
                theme.TextPrimary,
                theme.TextSecondary
            };

            Assert.IsTrue(
                colors.All(color => ArgbPattern.IsMatch(color)),
                $"Theme '{theme.Id}' contains a color that is not uppercase eight-digit ARGB.");
        }
    }

    [TestMethod]
    public void Catalog_ContentLensesAreDarkerThanApprovedLuminanceLimit()
    {
        var maximumLuminance = RelativeLuminance("#B34C344F");

        foreach (var theme in ThemeCatalog.All)
        {
            var actualLuminance = RelativeLuminance(theme.ContentLens);

            Assert.IsTrue(
                actualLuminance < maximumLuminance,
                $"Theme '{theme.Id}' content lens is too bright.");
        }
    }

    [TestMethod]
    public void DefaultSettings_MatchApprovedShellDefaults()
    {
        Assert.AreEqual(
            new ShellSettings(
                new AppearanceSettings("rose-purple", 0.74, 32, 0.72, false),
                new MotionSettings(PetalLevel.Low, false),
                new InputSettings(true, true, [])),
            ShellSettings.Default);
    }

    [TestMethod]
    public async Task InputSettings_ExposeAndPersistAnOptionalCustomShortcut()
    {
        var property = typeof(InputSettings).GetProperty("CustomShortcut");

        Assert.IsNotNull(property);
        Assert.AreEqual(typeof(string), property.PropertyType);
        Assert.IsNull(property.GetValue(ShellSettings.Default.Input));

        var directory = Path.Combine(Path.GetTempPath(), $"huahai-shortcut-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                path,
                """
                {"Appearance":{"ThemeId":"rose-purple","Opacity":0.88,"BlurAmount":32,"ReflectionStrength":0.72,"CompactMode":false},"Motion":{"PetalLevel":1,"ReduceMotion":false,"ClickDurationMs":760,"ReducedClickDurationMs":120},"Input":{"RightDoubleClickEnabled":true,"HotkeyEnabled":true,"ExcludedApplications":[],"CustomShortcut":"Ctrl + Alt + H"}}
                """);

            var loaded = await new JsonSettingsStore(path).LoadAsync(CancellationToken.None);
            Assert.AreEqual("Ctrl + Alt + H", property.GetValue(loaded.Input));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static double RelativeLuminance(string argb)
    {
        var red = ParseChannel(argb, 3);
        var green = ParseChannel(argb, 5);
        var blue = ParseChannel(argb, 7);

        return (0.2126 * Linearize(red))
            + (0.7152 * Linearize(green))
            + (0.0722 * Linearize(blue));
    }

    private static double ParseChannel(string argb, int startIndex) =>
        int.Parse(
            argb.AsSpan(startIndex, 2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture) / 255d;

    private static double Linearize(double channel) =>
        channel <= 0.04045
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
