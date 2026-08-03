namespace HuahaiClipboard.Core.Visual;

public sealed record VisualEnvironment(
    bool IsWindows11,
    bool IsHighContrast,
    bool IsReducedMotion,
    bool IsRemoteSession,
    bool IsEnergySaver);
