using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class TransientWindowVisibilityControllerTests
{
    [TestMethod]
    public void Show_MakesWindowTopmostBeforeShowingIt()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);

        controller.Show();

        CollectionAssert.AreEqual(
            new[] { "content:active", "topmost:on", "show" },
            host.Actions);
    }

    [TestMethod]
    public void Hide_RemovesTopmostBeforeHidingWindow()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);

        controller.Hide();

        CollectionAssert.AreEqual(
            new[] { "topmost:off", "hide", "content:suspended" },
            host.Actions);
    }

    [TestMethod]
    public async Task ShowAsync_WaitsForSynchronizationBeforeShowing()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);
        var synchronizationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSynchronization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var showTask = controller.ShowAsync(async cancellationToken =>
        {
            synchronizationStarted.SetResult();
            await releaseSynchronization.Task.WaitAsync(cancellationToken);
        }, TimeSpan.FromSeconds(1));

        await synchronizationStarted.Task;

        CollectionAssert.AreEqual(
            new[] { "content:active", "topmost:on" },
            host.Actions);
        Assert.IsFalse(showTask.IsCompleted);

        releaseSynchronization.SetResult();
        Assert.IsNull(await showTask);
        CollectionAssert.AreEqual(
            new[] { "content:active", "topmost:on", "show" },
            host.Actions);
    }

    [DataTestMethod]
    [DataRow(true, false, true)]
    [DataRow(false, false, false)]
    [DataRow(true, true, false)]
    public void HideOnDeactivated_RequiresTheSettingAndNoActiveManipulation(
        bool enabled,
        bool interactionActive,
        bool expectedHidden)
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);

        var hidden = controller.HideOnDeactivated(enabled, interactionActive);

        Assert.AreEqual(expectedHidden, hidden);
        if (expectedHidden)
        {
            CollectionAssert.AreEqual(
                new[] { "topmost:off", "hide", "content:suspended" },
                host.Actions);
        }
        else
        {
            Assert.AreEqual(0, host.Actions.Length);
        }
    }

    [TestMethod]
    public async Task HiddenBurst_AppliesTheLatestHistoryBeforeTheFirstVisibleFrame()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestedCount = 1;
        var appliedCount = 0;
        var refreshCalls = 0;
        var refresh = new LatestOnlyAsyncRefresh(async cancellationToken =>
        {
            if (Interlocked.Increment(ref refreshCalls) == 1)
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
            }

            appliedCount = Volatile.Read(ref requestedCount);
        });

        _ = refresh.RequestAsync();
        await firstEntered.Task;
        for (var count = 2; count <= 10; count++)
        {
            Volatile.Write(ref requestedCount, count);
            _ = refresh.RequestAsync();
        }

        releaseFirst.SetResult();
        await controller.ShowAsync(async () =>
        {
            await refresh.FlushAsync();
            host.Record($"state:{appliedCount}");
        });

        CollectionAssert.AreEqual(
            new[] { "content:active", "topmost:on", "state:10", "show" },
            host.Actions);
        Assert.AreEqual(2, refreshCalls);
    }

    [TestMethod]
    public async Task ShowAsync_WhenSynchronizationFails_ShowsTheLastValidContentAndReturnsTheError()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);

        var error = await controller.ShowAsync(
            _ => throw new InvalidOperationException("history unavailable"),
            TimeSpan.FromSeconds(1));

        Assert.IsInstanceOfType<InvalidOperationException>(error);
        CollectionAssert.AreEqual(
            new[] { "content:active", "topmost:on", "show" },
            host.Actions);
    }

    [TestMethod]
    public async Task ShowAsync_WhenSynchronizationTimesOut_ShowsWithoutWaitingForever()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);

        var error = await controller.ShowAsync(
            cancellationToken => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken),
            TimeSpan.FromMilliseconds(20));

        Assert.IsInstanceOfType<TimeoutException>(error);
        CollectionAssert.AreEqual(
            new[] { "content:active", "topmost:on", "show" },
            host.Actions);
    }

    private sealed class RecordingTransientWindowHost : ITransientWindowHost
    {
        public string[] Actions => actions.ToArray();

        private readonly List<string> actions = [];

        public void SetTopmost(bool enabled) =>
            actions.Add(enabled ? "topmost:on" : "topmost:off");

        public void SetContentActive(bool active) =>
            actions.Add(active ? "content:active" : "content:suspended");

        public void Show() => actions.Add("show");

        public void Hide() => actions.Add("hide");

        public void Record(string action) => actions.Add(action);
    }
}
