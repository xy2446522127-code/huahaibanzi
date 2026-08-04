using System.Globalization;
using System.Text.RegularExpressions;
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
            ["rose-purple"] = ("#FFE9A6D1", "#FFBC7CAF"),
            ["cobalt-blue"] = ("#FF77B5FF", "#FF4277D4"),
            ["emerald-cyan"] = ("#FF65DEC8", "#FF289B91"),
            ["amber-orange"] = ("#FFFFC26D", "#FFD57942"),
            ["aurora-cyan-purple"] = ("#FF7FE8E0", "#FF9B7DE3")
        };

        foreach (var theme in ThemeCatalog.All)
        {
            Assert.AreEqual(expected[theme.Id].Accent, theme.Accent, theme.Id);
            Assert.AreEqual(expected[theme.Id].Reflection, theme.Reflection, theme.Id);
            Assert.AreEqual("#FFFFFFFF", theme.TextPrimary, theme.Id);
            Assert.AreEqual("#CCFFFFFF", theme.TextSecondary, theme.Id);
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
                new AppearanceSettings("rose-purple", 0.86, 32, 0.72, false),
                new MotionSettings(PetalLevel.Low, false),
                new InputSettings(true, true, [])),
            ShellSettings.Default);
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
