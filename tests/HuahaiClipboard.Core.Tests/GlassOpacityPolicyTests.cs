using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class GlassOpacityPolicyTests
{
    [DataTestMethod]
    [DataRow(0.50, 0.65)]
    [DataRow(0.82, 0.82)]
    [DataRow(1.00, 0.96)]
    public void Normalize_ClampsSavedOpacityToTheVisibleSliderRange(double value, double expected)
    {
        Assert.AreEqual(expected, GlassOpacityPolicy.Normalize(value), 0.001);
    }
}
