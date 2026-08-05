using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using HuahaiClipboard.NativeUiSpike.Presentation.Windows;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class UiResourceLoadingTests
{
    [STATestMethod]
    public void GeneratedDictionary_LoadsLockedPanelGeometry()
    {
        _ = Application.Current ?? new Application();
        var dictionary = new ResourceDictionary
        {
            Source = new Uri(
                "/HuahaiClipboard.NativeUiSpike;component/Generated/HuahaiUiTokens.xaml",
                UriKind.Relative),
        };

        Assert.AreEqual(430d, dictionary["HuahaiPanelWidth"]);
        Assert.AreEqual(680d, dictionary["HuahaiPanelHeight"]);
        Assert.AreEqual(new CornerRadius(29), dictionary["HuahaiPanelCornerRadius"]);
        Assert.AreEqual(5, dictionary["HuahaiThemeCount"]);
    }

    [STATestMethod]
    public void GlassOpacity_ChangesMaterialWithoutFadingPanelContent()
    {
        _ = Application.Current ?? new Application();
        EnsureApplicationResources();
        var window = new MainWindow();

        window.SetGlassMaterialOpacity(0.73);

        Assert.AreEqual(0.73, window.GlassMaterialOpacity, 0.001);
        Assert.AreEqual(1, window.PanelContentOpacity, 0.001);
    }

    [STATestMethod]
    public void SettingsSurface_UsesApprovedEightTwentyBySixFiftyGeometry()
    {
        _ = Application.Current ?? new Application();
        EnsureApplicationResources();
        var window = new MainWindow();

        window.ShowSettings();

        Assert.AreEqual(820, window.Width, 0.001);
        Assert.AreEqual(650, window.Height, 0.001);
    }

    private static void EnsureApplicationResources()
    {
        if (Application.Current.Resources.Contains("HuahaiTextBoxStyle")) return;

        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/HuahaiClipboard.NativeUiSpike;component/Generated/HuahaiUiTokens.xaml",
                UriKind.Relative),
        });
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/HuahaiClipboard.NativeUiSpike;component/Presentation/Styles/NativeUiStyles.xaml",
                UriKind.Relative),
        });
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/HuahaiClipboard.NativeUiSpike;component/Presentation/Controls/GlassSurface.xaml",
                UriKind.Relative),
        });
    }
}
