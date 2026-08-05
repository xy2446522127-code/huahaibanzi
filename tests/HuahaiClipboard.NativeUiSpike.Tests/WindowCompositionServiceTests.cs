using HuahaiClipboard.NativeUiSpike.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class WindowCompositionServiceTests
{
    [DataTestMethod]
    [DataRow(19045, false)]
    [DataRow(22000, true)]
    [DataRow(26100, true)]
    public void UsesSystemRoundedCornersOnlyWhereDwmSupportsThem(int windowsBuild, bool expected)
    {
        Assert.AreEqual(expected, WindowCompositionService.UsesSystemRoundedCorners(windowsBuild));
    }

    [DataTestMethod]
    [DataRow(19045)]
    [DataRow(22000)]
    [DataRow(26100)]
    public void CustomRoundedRegion_IsAppliedOnWindowsTenAndEleven(int windowsBuild)
    {
        Assert.IsTrue(WindowCompositionService.ShouldApplyCustomRoundedRegion(windowsBuild));
    }
}
