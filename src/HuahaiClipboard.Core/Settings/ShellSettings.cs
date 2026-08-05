namespace HuahaiClipboard.Core.Settings;

public sealed record ShellSettings(
    AppearanceSettings Appearance,
    MotionSettings Motion,
    InputSettings Input)
{
    public BehaviorSettings Behavior { get; init; } = BehaviorSettings.Default;

    public static ShellSettings Default => new(
        new AppearanceSettings(
            ThemeId: "rose-purple",
            Opacity: 0.74,
            BlurAmount: 32,
            ReflectionStrength: 0.72,
            CompactMode: false),
        new MotionSettings(
            PetalLevel: PetalLevel.Low,
            ReduceMotion: false),
        new InputSettings(
            RightDoubleClickEnabled: true,
            HotkeyEnabled: true,
            ExcludedApplications: []))
        {
            Behavior = BehaviorSettings.Default
        };
}
