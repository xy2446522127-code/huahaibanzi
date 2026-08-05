using HuahaiClipboard.NativeUiSpike.Production;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class ProductionCompositionRootTests
{
    [TestMethod]
    public void UsesTheExistingCompatibleLocalDataLayout()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), $"huahai-composition-{Guid.NewGuid():N}");

        var root = new ProductionCompositionRoot(baseDirectory);

        Assert.AreEqual(Path.Combine(baseDirectory, "HuahaiClipboard"), root.DataLayout.DataDirectory);
        Assert.IsNotNull(root.CreateViewModel());
    }

    [TestMethod]
    public void DefaultRootHonorsTheIsolatedDataOverride()
    {
        var previous = Environment.GetEnvironmentVariable("HUAHAI_CLIPBOARD_LOCALAPPDATA");
        var isolated = Path.Combine(Path.GetTempPath(), $"huahai-isolated-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_LOCALAPPDATA", isolated);

            var root = new ProductionCompositionRoot();

            Assert.AreEqual(Path.Combine(isolated, "HuahaiClipboard"), root.DataLayout.DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_LOCALAPPDATA", previous);
        }
    }
}
