namespace HuahaiClipboard.Core.Services;

public interface ITransientWindowHost
{
    void SetContentActive(bool active);

    void SetTopmost(bool enabled);

    void Show();

    void Hide();
}

public sealed class TransientWindowVisibilityController(ITransientWindowHost host)
{
    public void Show()
    {
        host.SetContentActive(true);
        host.SetTopmost(true);
        host.Show();
    }

    public async Task ShowAsync(Func<Task> synchronizeBeforeShow)
    {
        ArgumentNullException.ThrowIfNull(synchronizeBeforeShow);
        host.SetContentActive(true);
        host.SetTopmost(true);
        await synchronizeBeforeShow();
        host.Show();
    }

    public async Task<Exception?> ShowAsync(
        Func<CancellationToken, Task> synchronizeBeforeShow,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(synchronizeBeforeShow);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        host.SetContentActive(true);
        host.SetTopmost(true);
        Exception? synchronizationError = null;
        using var timeoutSource = new CancellationTokenSource(timeout);
        try
        {
            await synchronizeBeforeShow(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            synchronizationError = new TimeoutException(
                $"Pre-show synchronization exceeded {timeout.TotalMilliseconds:0} ms.");
        }
        catch (Exception exception)
        {
            synchronizationError = exception;
        }

        host.Show();
        return synchronizationError;
    }

    public void Hide()
    {
        host.SetTopmost(false);
        host.Hide();
        host.SetContentActive(false);
    }

    public bool HideOnDeactivated(bool enabled, bool interactionActive)
    {
        if (!enabled || interactionActive)
        {
            return false;
        }

        Hide();
        return true;
    }
}
