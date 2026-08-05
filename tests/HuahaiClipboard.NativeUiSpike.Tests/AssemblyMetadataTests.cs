using System.Reflection;
using HuahaiClipboard.NativeUiSpike.Presentation.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class AssemblyMetadataTests
{
    [TestMethod]
    public void ProductionAssembly_UsesHuahaiClipboardProductIdentity()
    {
        var assembly = typeof(MainWindow).Assembly;

        Assert.AreEqual(
            "花海剪贴板",
            assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
        Assert.AreEqual(
            "花海剪贴板",
            assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.AreEqual(
            "HuahaiClipboard",
            assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company);
    }
}
