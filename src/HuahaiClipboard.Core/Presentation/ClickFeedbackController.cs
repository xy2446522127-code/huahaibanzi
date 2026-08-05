namespace HuahaiClipboard.Core.Presentation;

public sealed class ClickFeedbackController
{
    public Task RunAsync(
        bool reduceMotion,
        Func<TimeSpan, CancellationToken, Task> playAnimationAsync,
        Func<CancellationToken, Task> actionAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playAnimationAsync);
        ArgumentNullException.ThrowIfNull(actionAsync);
        return RunCoreAsync(reduceMotion, playAnimationAsync, actionAsync, cancellationToken);
    }

    private static async Task RunCoreAsync(
        bool reduceMotion,
        Func<TimeSpan, CancellationToken, Task> playAnimationAsync,
        Func<CancellationToken, Task> actionAsync,
        CancellationToken cancellationToken)
    {
        var duration = TimeSpan.FromMilliseconds(reduceMotion ? 120 : 620);
        await actionAsync(cancellationToken);
        await playAnimationAsync(duration, cancellationToken);
    }
}
