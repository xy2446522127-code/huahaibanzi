using System.Security.Principal;

namespace HuahaiClipboard.Core.Services;

public sealed class LocalDataLayout
{
    private const string InstallRootOverride = "HUAHAI_CLIPBOARD_INSTALL_ROOT";
    private const string UserKeyOverride = "HUAHAI_CLIPBOARD_USER_KEY";
    private const string LegacyLocalAppDataOverride = "HUAHAI_CLIPBOARD_LOCALAPPDATA";
    private const string DefaultUserKey = "default-user";
    private static readonly HashSet<string> ReservedWindowsFileNames = new(
        ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"],
        StringComparer.OrdinalIgnoreCase);

    public static string ResolveInstallRoot()
    {
        var explicitRoot = Environment.GetEnvironmentVariable(InstallRootOverride);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return Path.GetFullPath(explicitRoot);
        }

        // Existing smoke-test callers use this variable as a private filesystem sandbox.
        var legacyTestRoot = Environment.GetEnvironmentVariable(LegacyLocalAppDataOverride);
        return string.IsNullOrWhiteSpace(legacyTestRoot)
            ? Path.GetFullPath(AppContext.BaseDirectory)
            : Path.GetFullPath(legacyTestRoot);
    }

    public static string ResolveUserKey()
    {
        var explicitKey = Environment.GetEnvironmentVariable(UserKeyOverride);
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return NormalizeUserKey(explicitKey);
        }

        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value;
            if (!string.IsNullOrWhiteSpace(sid))
            {
                return NormalizeUserKey(sid);
            }
        }

        return NormalizeUserKey(Environment.UserName);
    }

    public static string ResolveLegacyDataDirectory()
    {
        var localApplicationData = Environment.GetEnvironmentVariable(LegacyLocalAppDataOverride);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(Path.GetFullPath(localApplicationData), "HuahaiClipboard");
    }

    public static async Task<DataRootResolution> ResolveDataRootAsync(
        IDataLocationRegistry registry,
        string installRoot,
        string legacyDataDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyDataDirectory);

        var registeredDataRoot = NormalizeExistingDirectory(
            await registry.ReadAsync(cancellationToken));
        var installDataRoot = NormalizeDirectory(Path.Combine(installRoot, "Data"));
        var populatedRoots = new[] { registeredDataRoot, installDataRoot }
            .Where(root => root is not null && HasRecognizedPerUserData(root))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var legacySource = HasRecognizedLegacyData(legacyDataDirectory)
            ? NormalizeDirectory(legacyDataDirectory)
            : null;

        if (populatedRoots.Length > 1)
        {
            return new DataRootResolution(
                DataRootResolutionKind.RecoveryRequired,
                null,
                legacySource,
                populatedRoots);
        }

        if (registeredDataRoot is not null &&
            (HasRecognizedPerUserData(registeredDataRoot) || !HasRecognizedPerUserData(installDataRoot)))
        {
            return new DataRootResolution(
                DataRootResolutionKind.Registered,
                registeredDataRoot,
                legacySource,
                []);
        }

        if (HasRecognizedPerUserData(installDataRoot))
        {
            return new DataRootResolution(
                DataRootResolutionKind.InstallRoot,
                installDataRoot,
                legacySource,
                []);
        }

        return new DataRootResolution(
            DataRootResolutionKind.NewInstall,
            installDataRoot,
            legacySource,
            []);
    }

    public LocalDataLayout(string installRoot, string userKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(userKey);
        InstallRoot = Path.GetFullPath(installRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        DataRoot = Path.Combine(InstallRoot, "Data");
        DataDirectory = Path.GetFullPath(Path.Combine(DataRoot, NormalizeUserKey(userKey)));
        if (!string.Equals(
                Path.GetDirectoryName(DataDirectory),
                Path.GetFullPath(DataRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The user data directory must be a direct child of Data.");
        }
        HistoryFile = Path.Combine(DataDirectory, "history.dat");
        SettingsFile = Path.Combine(DataDirectory, "settings.json");
        WindowPositionsFile = Path.Combine(DataDirectory, "window-positions.json");
        ImageDirectory = Path.Combine(DataDirectory, "images");
        TodoWorkspaceFile = Path.Combine(DataDirectory, "todo-workspace.json");
        TodoImageDirectory = Path.Combine(DataDirectory, "todo-images");
    }

    public string InstallRoot { get; }

    public string DataRoot { get; }

    public string DataDirectory { get; }

    public string HistoryFile { get; }

    public string SettingsFile { get; }

    public string WindowPositionsFile { get; }

    public string ImageDirectory { get; }

    public string TodoWorkspaceFile { get; }

    public string TodoImageDirectory { get; }

    private static string NormalizeUserKey(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)
            || trimmed is "." or ".."
            || value.EndsWith(' ')
            || value.EndsWith('.'))
        {
            return DefaultUserKey;
        }

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var normalized = new string(trimmed
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray());
        var baseName = normalized.Split('.', 2)[0];
        return string.IsNullOrWhiteSpace(normalized)
            || normalized is "." or ".."
            || ReservedWindowsFileNames.Contains(baseName)
                ? DefaultUserKey
                : normalized;
    }

    private static string? NormalizeExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = NormalizeDirectory(path);
        return Directory.Exists(normalized) ? normalized : null;
    }

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool HasRecognizedPerUserData(string? dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot) || !Directory.Exists(dataRoot)) return false;
        try
        {
            return Directory.EnumerateDirectories(dataRoot)
                .Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
                .Any(path => HasKnownDataFile(path));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasRecognizedLegacyData(string legacyDirectory) =>
        Directory.Exists(legacyDirectory) && HasKnownDataFile(legacyDirectory);

    private static bool HasKnownDataFile(string directory)
    {
        return File.Exists(Path.Combine(directory, "history.dat")) ||
               File.Exists(Path.Combine(directory, "settings.json")) ||
               File.Exists(Path.Combine(directory, "todo-workspace.json")) ||
               File.Exists(Path.Combine(directory, "window-positions.json"));
    }
}
