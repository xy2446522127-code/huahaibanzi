namespace HuahaiClipboard.Core.Services;

public sealed class LocalDataLayout
{
    public static string ResolveBaseDirectory() =>
        Environment.GetEnvironmentVariable("HUAHAI_CLIPBOARD_LOCALAPPDATA") ??
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public LocalDataLayout(string localApplicationData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        DataDirectory = Path.Combine(localApplicationData, "HuahaiClipboard");
        HistoryFile = Path.Combine(DataDirectory, "history.dat");
        SettingsFile = Path.Combine(DataDirectory, "settings.json");
        WindowPositionsFile = Path.Combine(DataDirectory, "window-positions.json");
        ImageDirectory = Path.Combine(DataDirectory, "images");
    }

    public string DataDirectory { get; }

    public string HistoryFile { get; }

    public string SettingsFile { get; }

    public string WindowPositionsFile { get; }

    public string ImageDirectory { get; }
}
