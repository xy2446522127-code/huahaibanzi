using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class SuspendResumeActivityCoordinatorTests
{
    [TestMethod]
    public async Task ImmediateReactivation_WaitsForPendingSuspendAndMarksItStaleBeforeDomWork()
    {
        var coordinator = new SuspendResumeActivityCoordinator();
        var suspendEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSuspend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleSuspendRecovered = false;

        var suspend = coordinator.RequestSuspendAsync(async version =>
        {
            suspendEntered.SetResult();
            await releaseSuspend.Task;
            staleSuspendRecovered = !coordinator.IsCurrent(version);
        });
        await suspendEntered.Task;

        coordinator.MarkActive();
        var readyForDom = coordinator.WaitForPendingSuspendAsync();
        Assert.IsFalse(readyForDom.IsCompleted);
        releaseSuspend.SetResult();

        await Task.WhenAll(suspend, readyForDom);
        Assert.IsTrue(staleSuspendRecovered);
    }

    [TestMethod]
    public async Task RepeatedSuspendRequests_AreSerializedAndFlushWaitsForAllOfThem()
    {
        var coordinator = new SuspendResumeActivityCoordinator();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var maximumRunning = 0;

        var first = coordinator.RequestSuspendAsync(async _ =>
        {
            maximumRunning = Math.Max(maximumRunning, Interlocked.Increment(ref running));
            firstEntered.SetResult();
            await releaseFirst.Task;
            Interlocked.Decrement(ref running);
        });
        await firstEntered.Task;
        var second = coordinator.RequestSuspendAsync(_ =>
        {
            maximumRunning = Math.Max(maximumRunning, Interlocked.Increment(ref running));
            Interlocked.Decrement(ref running);
            return Task.CompletedTask;
        });
        var flush = coordinator.WaitForPendingSuspendAsync();

        Assert.IsFalse(flush.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second, flush);
        Assert.AreEqual(1, maximumRunning);
    }
}
