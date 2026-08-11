using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class LatestOnlyAsyncRefreshTests
{
    [TestMethod]
    public async Task TenRequestsDuringARefresh_CoalesceWithoutConcurrentExecutionAndApplyTheLatestValue()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestedValue = 1;
        var appliedValue = 0;
        var calls = 0;
        var running = 0;
        var maximumRunning = 0;
        var coordinator = new LatestOnlyAsyncRefresh(async cancellationToken =>
        {
            var currentRunning = Interlocked.Increment(ref running);
            maximumRunning = Math.Max(maximumRunning, currentRunning);
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }

            appliedValue = Volatile.Read(ref requestedValue);
            Interlocked.Decrement(ref running);
        });

        var first = coordinator.RequestAsync();
        await firstEntered.Task;
        var requests = new List<Task> { first };
        for (var value = 2; value <= 11; value++)
        {
            Volatile.Write(ref requestedValue, value);
            requests.Add(coordinator.RequestAsync());
        }

        releaseFirst.SetResult();
        await Task.WhenAll(requests);
        await coordinator.FlushAsync();

        Assert.AreEqual(2, calls, "One active refresh plus one latest-state refresh is sufficient.");
        Assert.AreEqual(1, maximumRunning, "Refresh work must never run concurrently.");
        Assert.AreEqual(11, appliedValue, "The final refresh must observe the latest requested state.");
    }

    [TestMethod]
    public async Task FlushAsync_WaitsForThePendingRefresh()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new LatestOnlyAsyncRefresh(async cancellationToken =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
        });

        _ = coordinator.RequestAsync();
        await entered.Task;
        var flush = coordinator.FlushAsync();

        Assert.IsFalse(flush.IsCompleted);
        release.SetResult();
        await flush;
    }

    [TestMethod]
    public async Task CancellingOneWaiter_DoesNotCancelTheSharedRefreshOrOtherWaiters()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new LatestOnlyAsyncRefresh(async _ =>
        {
            entered.TrySetResult();
            await release.Task;
        });
        using var cancellation = new CancellationTokenSource();

        var cancelledWaiter = coordinator.RequestAsync(cancellation.Token);
        await entered.Task;
        var survivingWaiter = coordinator.RequestAsync();
        cancellation.Cancel();

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => cancelledWaiter);
        Assert.IsFalse(survivingWaiter.IsCompleted);
        release.SetResult();
        await survivingWaiter;
        await coordinator.FlushAsync();
    }

    [TestMethod]
    public async Task FailedRefresh_DoesNotPoisonTheNextRequest()
    {
        var calls = 0;
        var coordinator = new LatestOnlyAsyncRefresh(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("first refresh failed");
            }

            return Task.CompletedTask;
        });

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => coordinator.RequestAsync());

        await coordinator.RequestAsync();
        await coordinator.FlushAsync();
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task RequestArrivingWhileCurrentRefreshFails_IsAppliedByASuccessorRefresh()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var coordinator = new LatestOnlyAsyncRefresh(async _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstEntered.SetResult();
                await releaseFailure.Task;
                throw new InvalidOperationException("stale refresh failed");
            }
        });

        var first = coordinator.RequestAsync();
        await firstEntered.Task;
        var newer = coordinator.RequestAsync();
        releaseFailure.SetResult();

        await Task.WhenAll(first, newer);
        await coordinator.FlushAsync();
        Assert.AreEqual(2, calls);
    }
}
