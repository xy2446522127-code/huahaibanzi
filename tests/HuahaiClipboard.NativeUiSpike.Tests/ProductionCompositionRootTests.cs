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
        var previousUserKey = Environment.GetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY");
        try
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY", "composition-user");
            var root = new ProductionCompositionRoot(baseDirectory);

            Assert.AreEqual(Path.Combine(baseDirectory, "Data", "composition-user"), root.DataLayout.DataDirectory);
            Assert.IsNotNull(root.CreateViewModel());
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY", previousUserKey);
        }
    }

    [TestMethod]
    public void DefaultRootHonorsTheIsolatedDataOverride()
    {
        var previous = Environment.GetEnvironmentVariable("HUAHAI_CLIPBOARD_LOCALAPPDATA");
        var previousUserKey = Environment.GetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY");
        var isolated = Path.Combine(Path.GetTempPath(), $"huahai-isolated-{Guid.NewGuid():N}");
        try
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_LOCALAPPDATA", isolated);
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY", "isolated-user");

            var root = new ProductionCompositionRoot();

            Assert.AreEqual(Path.Combine(isolated, "Data", "isolated-user"), root.DataLayout.DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_LOCALAPPDATA", previous);
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY", previousUserKey);
        }
    }
}
