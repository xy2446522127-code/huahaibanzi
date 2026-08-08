using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ProactiveUpdateCoordinatorTests
{
    private static readonly UpdateCheckResult NoUpdate = new(
        false,
        new Version(1, 1, 7),
        new Version(1, 1, 7),
        GitHubUpdateCheckService.ReleasesPage,
        GitHubUpdateCheckService.InstallerAssetName,
        string.Empty,
        0,
        string.Empty);

    [TestMethod]
    public async Task StartsWithAnImmediateCheckThenUsesTheFiveMinuteInterval()
    {
        var checkedSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delaySignal = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new ProactiveUpdateCoordinator(
            _ => Task.FromResult(true),
            _ =>
            {
                checkedSignal.TrySetResult();
                return Task.FromResult(NoUpdate);
            },
            (_, _) => Task.CompletedTask,
            delayAsync: (delay, token) =>
            {
                delaySignal.TrySetResult(delay);
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        coordinator.Start();

        await checkedSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(
            UpdateReminderPolicy.PollInterval,
            await delaySignal.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public async Task FailureBackoffMovesFromFifteenToSixtyMinutes()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new ProactiveUpdateCoordinator(
            _ => Task.FromResult(true),
            _ => throw new HttpRequestException("offline"),
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask,
            (delay, token) =>
            {
                delays.Add(delay);
                attempts++;
                if (attempts == 2)
                {
                    observed.TrySetResult();
                    return Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                return Task.CompletedTask;
            });

        coordinator.Start();

        await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromMinutes(15), TimeSpan.FromHours(1) },
            delays);
    }

    [TestMethod]
    public async Task DuplicateStartDoesNotCreateOverlappingLoops()
    {
        var activeChecks = 0;
        var maximumActiveChecks = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var coordinator = new ProactiveUpdateCoordinator(
            _ => Task.FromResult(true),
            async token =>
            {
                maximumActiveChecks = Math.Max(maximumActiveChecks, Interlocked.Increment(ref activeChecks));
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                Interlocked.Decrement(ref activeChecks);
                return NoUpdate;
            },
            (_, _) => Task.CompletedTask);

        coordinator.Start();
        coordinator.Start();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.TrySetResult();

        Assert.AreEqual(1, maximumActiveChecks);
    }
}
