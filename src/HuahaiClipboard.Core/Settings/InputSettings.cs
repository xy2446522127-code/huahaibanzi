namespace HuahaiClipboard.Core.Settings;

public sealed record InputSettings(
    bool RightDoubleClickEnabled,
    bool HotkeyEnabled,
    string[] ExcludedApplications);
