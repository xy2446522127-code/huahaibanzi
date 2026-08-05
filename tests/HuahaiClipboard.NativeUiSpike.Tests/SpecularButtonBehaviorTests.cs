using System.Windows;
using HuahaiClipboard.NativeUiSpike.Presentation.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class SpecularButtonBehaviorTests
{
    [DataTestMethod]
    [DataRow(50, 20, 1.0)]
    [DataRow(105, 20, 0.5)]
    [DataRow(110, 20, 0.0)]
    [DataRow(111, 20, 0.0)]
    public void CalculateIntensity_UsesTenPixelProximity(double x, double y, double expected)
    {
        var buttonBounds = new Rect(0, 0, 100, 40);

        var actual = SpecularButtonBehavior.CalculateIntensity(buttonBounds, new Point(x, y), 10);

        Assert.AreEqual(expected, actual, 0.001);
    }
}
