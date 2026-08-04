namespace HuahaiClipboard.Core.Services;

public static class UnmanagedCallbackGuard
{
    public static T Invoke<T>(Func<T> callback, Func<T> fallback)
    {
        try
        {
            return callback();
        }
        catch
        {
            return fallback();
        }
    }

    public static async Task InvokeAsync(Func<Task> callback, Action<Exception>? report = null)
    {
        try
        {
            await callback();
        }
        catch (Exception exception)
        {
            report?.Invoke(exception);
        }
    }
}
