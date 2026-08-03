using HuahaiClipboard.Core.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class VisualModeResolverTests
{
    [TestMethod]
    public void Resolver_UsesFullModeOnUnconstrainedWindows11()
    {
        var environment = new VisualEnvironment(
            IsWindows11: true,
            IsHighContrast: false,
            IsReducedMotion: false,
            IsRemoteSession: false,
            IsEnergySaver: false);

        Assert.AreEqual(VisualMode.Full, VisualModeResolver.Resolve(environment));
    }

    [TestMethod]
    public void Resolver_UsesReducedModeOnWindows10()
    {
        var environment = new VisualEnvironment(
            IsWindows11: false,
            IsHighContrast: false,
            IsReducedMotion: false,
            IsRemoteSession: false,
            IsEnergySaver: false);

        Assert.AreEqual(VisualMode.Reduced, VisualModeResolver.Resolve(environment));
    }

    [TestMethod]
    public void Resolver_UsesStaticModeForHighContrast()
    {
        Assert.AreEqual(
            VisualMode.Static,
            VisualModeResolver.Resolve(new(true, true, false, false, false)));
    }

    [TestMethod]
    public void Resolver_UsesStaticModeForReducedMotion()
    {
        Assert.AreEqual(
            VisualMode.Static,
            VisualModeResolver.Resolve(new(true, false, true, false, false)));
    }

    [TestMethod]
    public void Resolver_UsesStaticModeForRemoteSessions()
    {
        Assert.AreEqual(
            VisualMode.Static,
            VisualModeResolver.Resolve(new(true, false, false, true, false)));
    }

    [TestMethod]
    public void Resolver_UsesStaticModeForEnergySaver()
    {
        Assert.AreEqual(
            VisualMode.Static,
            VisualModeResolver.Resolve(new(true, false, false, false, true)));
    }
}
