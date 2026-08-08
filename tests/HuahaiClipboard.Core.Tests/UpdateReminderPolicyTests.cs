using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UpdateReminderPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void SuppressesTheSameVersionUntilTheSnoozeDeadline()
    {
        Assert.IsFalse(UpdateReminderPolicy.ShouldNotify(
            new Version(1, 1, 7), "1.1.7", Now.AddMinutes(1), Now));
    }

    [TestMethod]
    public void AllowsTheSameVersionAfterTheSnoozeDeadline()
    {
        Assert.IsTrue(UpdateReminderPolicy.ShouldNotify(
            new Version(1, 1, 7), "1.1.7", Now, Now));
    }

    [TestMethod]
    public void ANewerVersionIsNotSuppressedByAnOlderVersionSnooze()
    {
        Assert.IsTrue(UpdateReminderPolicy.ShouldNotify(
            new Version(1, 1, 8), "1.1.7", Now.AddHours(1), Now));
    }

    [TestMethod]
    public void UsesTheApprovedPollingBackoffAndSnoozeIntervals()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(5), UpdateReminderPolicy.PollInterval);
        Assert.AreEqual(TimeSpan.FromMinutes(15), UpdateReminderPolicy.DelayAfterFailure(1));
        Assert.AreEqual(TimeSpan.FromHours(1), UpdateReminderPolicy.DelayAfterFailure(2));
        Assert.AreEqual(TimeSpan.FromHours(24), UpdateReminderPolicy.SnoozeDuration);
    }
}
