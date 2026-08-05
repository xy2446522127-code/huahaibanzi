using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

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
}
