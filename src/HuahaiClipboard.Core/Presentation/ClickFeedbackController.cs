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
        var duration = TimeSpan.FromMilliseconds(reduceMotion ? 120 : 760);
        await playAnimationAsync(duration, cancellationToken);
        await actionAsync(cancellationToken);
    }
}
