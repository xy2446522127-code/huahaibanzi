using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class SerialAsyncWorkQueueTests
{
    [TestMethod]
    public async Task FlushAsync_WaitsForEveryOperationRegisteredBeforeItsSnapshotInFifoOrder()
    {
        var queue = new SerialAsyncWorkQueue();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<int>();

        var first = queue.EnqueueAsync(async () =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task;
            order.Add(1);
        });
        await firstEntered.Task;
        var second = queue.EnqueueAsync(() =>
        {
            order.Add(2);
            return Task.CompletedTask;
        });
        var third = queue.EnqueueAsync(() =>
        {
            order.Add(3);
            return Task.CompletedTask;
        });
        var flush = queue.FlushAsync();

        Assert.IsFalse(flush.IsCompleted);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second, third, flush);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
    }

    [TestMethod]
    public async Task FailedOperation_DoesNotPreventLaterRegisteredWorkFromRunning()
    {
        var queue = new SerialAsyncWorkQueue();
        var laterRan = false;

        var failed = queue.EnqueueAsync(() => throw new InvalidOperationException("capture failed"));
        var later = queue.EnqueueAsync(() =>
        {
            laterRan = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => failed);
        await later;
        await queue.FlushAsync();
        Assert.IsTrue(laterRan);
    }
}
