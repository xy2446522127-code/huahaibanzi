using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UpdateNotificationSessionTests
{
    private static readonly Version Release = new(1, 1, 7);
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void SnoozedReleaseCanNotifyAgainAfterDeadlineInTheSameProcess()
    {
        var session = new UpdateNotificationSession();

        Assert.IsTrue(session.ShouldNotify(Release, null, null, Now));
        session.MarkNotified(Release);
        Assert.IsFalse(session.ShouldNotify(Release, null, null, Now));

        session.MarkSnoozed(Release);
        Assert.IsFalse(session.ShouldNotify(Release, "1.1.7", Now.AddHours(24), Now));
        Assert.IsTrue(session.ShouldNotify(Release, "1.1.7", Now, Now));

        session.MarkNotified(Release);
        Assert.IsFalse(session.ShouldNotify(Release, "1.1.7", Now, Now));
    }
}
