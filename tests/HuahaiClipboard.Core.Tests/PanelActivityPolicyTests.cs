using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class PanelActivityPolicyTests
{
    [DataTestMethod]
    [DataRow(false, false, PetalLevel.Low, false, false)]
    [DataRow(true, false, PetalLevel.Low, true, true)]
    [DataRow(true, true, PetalLevel.High, false, false)]
    [DataRow(true, false, PetalLevel.Off, true, false)]
    public void Resolve_RunsAmbientWorkOnlyWhileThePanelIsVisible(
        bool isPanelVisible,
        bool reduceMotion,
        PetalLevel petalLevel,
        bool expectedLiquidReflection,
        bool expectedPetals)
    {
        var activity = PanelActivityPolicy.Resolve(
            isPanelVisible,
            reduceMotion,
            petalLevel);

        Assert.AreEqual(expectedLiquidReflection, activity.AnimateLiquidReflection);
        Assert.AreEqual(expectedPetals, activity.AnimatePetals);
    }
}
