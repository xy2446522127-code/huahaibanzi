using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UiResourceContractTests
{
    [TestMethod]
    public void ResourceKeys_ArePresentExactlyOnce()
    {
        var repositoryRoot = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(repositoryRoot, "src", "HuahaiClipboard.App", "Resources", "Brushes.xaml"),
            Path.Combine(repositoryRoot, "src", "HuahaiClipboard.App", "Resources", "ControlStyles.xaml"),
            Path.Combine(repositoryRoot, "src", "HuahaiClipboard.App", "Resources", "Typography.xaml")
        };
        var combined = string.Join(Environment.NewLine, files.Select(File.ReadAllText));
        var requiredKeys = new[]
        {
            "HuahaiTextPrimaryBrush",
            "HuahaiTextSecondaryBrush",
            "HuahaiContentLensBrush",
            "HuahaiFocusBrush",
            "HuahaiRecordRowStyle"
        };

        foreach (var key in requiredKeys)
        {
            Assert.AreEqual(
                1,
                Regex.Matches(combined, $"x:Key=\"{Regex.Escape(key)}\"").Count,
                key);
        }
    }

    [TestMethod]
    public void ApprovedV6WebShellAndBrandAssets_ArePackagedWithTheExecutable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "HuahaiClipboard.App",
            "Presentation",
            "Windows",
            "CursorPanelWindow.xaml"));
        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "HuahaiClipboard.App",
            "HuahaiClipboard.App.csproj"));
        var webShellPath = Path.Combine(
            repositoryRoot,
            "src",
            "HuahaiClipboard.App",
            "Assets",
            "Web",
            "product-shell.html");
        var iconPath = Path.Combine(
            repositoryRoot,
            "src",
            "HuahaiClipboard.App",
            "Assets",
            "Brand",
            "fox-icon.ico");
        var imagePath = Path.Combine(
            repositoryRoot,
            "src",
            "HuahaiClipboard.App",
            "Assets",
            "Brand",
            "fox-icon-source.png");

        StringAssert.Contains(xaml, "WebView2 x:Name=\"ProductWebView\"");
        StringAssert.Contains(project, "Assets\\Web\\product-shell.html");
        StringAssert.Contains(project, "Assets\\Brand\\fox-icon-source.png");
        StringAssert.Contains(project, "<ApplicationIcon>Assets\\Brand\\fox-icon.ico</ApplicationIcon>");
        Assert.IsTrue(File.Exists(webShellPath), "The approved v6 production web shell is missing.");
        Assert.IsTrue(File.Exists(iconPath), "The EXE/tray icon asset is missing.");
        Assert.IsTrue(File.Exists(imagePath), "The approved fox brand image is missing.");

        var webShell = File.ReadAllText(webShellPath);
        StringAssert.Contains(webShell, "<section class=\"glass-panel\" id=\"glassPanel\">");
        StringAssert.Contains(webShell, "const specularProximity=10;");
        StringAssert.Contains(webShell, "window.chrome.webview");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HuahaiClipboard.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
