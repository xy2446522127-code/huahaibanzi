namespace HuahaiClipboard.Core.Visual;

public static class VisualModeResolver
{
    public static VisualMode Resolve(VisualEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsHighContrast
            || environment.IsReducedMotion
            || environment.IsRemoteSession
            || environment.IsEnergySaver)
        {
            return VisualMode.Static;
        }

        return environment.IsWindows11
            ? VisualMode.Full
            : VisualMode.Reduced;
    }
}
