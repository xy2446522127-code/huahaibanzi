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
