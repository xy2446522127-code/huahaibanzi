using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string PayloadResource = "HuahaiClipboard.Payload";

try
{
    if (args.Length == 0) throw new ArgumentException("A probe command is required.");
    var options = ParseOptions(args.Skip(1).ToArray());
    object result = args[0] switch
    {
        "extract" => ExtractCommand(options),
        "upgrade" => UpgradeCommand(options),
        "probe-update" => ProbeUpdateCommand(options),
        _ => throw new ArgumentException($"Unknown probe command: {args[0]}")
    };
    Console.WriteLine(JsonSerializer.Serialize(result));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static object ExtractCommand(IReadOnlyDictionary<string, string> options)
{
    var installer = RequiredPath(options, "installer", mustExist: true);
    var destination = RequiredPath(options, "destination", mustExist: false);
    ExtractInstaller(installer, destination);
    return new
    {
        status = "passed",
        version = ApplicationVersion(destination),
        fileCount = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Length
    };
}

static object UpgradeCommand(IReadOnlyDictionary<string, string> options)
{
    var oldInstaller = RequiredPath(options, "old-installer", mustExist: true);
    var newInstaller = RequiredPath(options, "new-installer", mustExist: true);
    var workRoot = RequiredPath(options, "work-root", mustExist: false);
    var expectedFrom = Required(options, "expected-from");
    var expectedTo = Required(options, "expected-to");
    if (Directory.Exists(workRoot)) Directory.Delete(workRoot, recursive: true);
    Directory.CreateDirectory(workRoot);

    var active = Path.Combine(workRoot, "active");
    var staging = Path.Combine(workRoot, "staging");
    var backup = Path.Combine(workRoot, "backup");
    ExtractInstaller(oldInstaller, active);
    ExtractInstaller(newInstaller, staging);
    RequireVersion(active, expectedFrom);
    RequireVersion(staging, expectedTo);

    var dataPath = Path.Combine(active, "Data", "evidence-user", "representative-data.json");
    Directory.CreateDirectory(Path.GetDirectoryName(dataPath)!);
    File.WriteAllText(dataPath, "{\"favorite\":true,\"pinned\":true,\"text\":\"花海升级证据\"}");
    var expectedDataHash = Sha256(dataPath);
    InstallDataPreserver.CopyIntoCandidate(active, staging);

    var swap = InstallSwapTransaction.Execute(
        staging,
        active,
        backup,
        Directory.Exists,
        MoveDirectoryWithRetry,
        path =>
        {
            if (!Directory.Exists(path)) return true;
            Directory.Delete(path, recursive: true);
            return !Directory.Exists(path);
        },
        () => RequireVersion(active, expectedTo));

    var preservedDataPath = Path.Combine(active, "Data", "evidence-user", "representative-data.json");
    if (!File.Exists(preservedDataPath) || !string.Equals(Sha256(preservedDataPath), expectedDataHash, StringComparison.Ordinal))
    {
        throw new InvalidDataException("Representative install-root data was not preserved.");
    }

    return new
    {
        status = "passed",
        fromVersion = expectedFrom,
        toVersion = expectedTo,
        dataPreserved = true,
        startupPayloadReady = RequiredPayloadExists(active),
        backupCleanupPending = swap.BackupCleanupPending
    };
}

static object ProbeUpdateCommand(IReadOnlyDictionary<string, string> options)
{
    var corePath = RequiredPath(options, "core", mustExist: true);
    var currentVersion = Version.Parse(Required(options, "current"));
    var expectedTarget = Version.Parse(Required(options, "expected-target"));
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(corePath);
    var serviceType = assembly.GetType("HuahaiClipboard.Core.Services.GitHubUpdateCheckService", throwOnError: true)!;
    HttpClient fixtureClient = null;
    try
    {
        object service;
        if (options.TryGetValue("release-fixture", out var fixturePath))
        {
            var fixture = File.ReadAllText(Path.GetFullPath(fixturePath));
            fixtureClient = new HttpClient(new StaticReleaseHandler(fixture));
            service = Activator.CreateInstance(serviceType, fixtureClient, currentVersion)!;
        }
        else
        {
            service = serviceType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)!
                .Invoke(null, [currentVersion])!;
        }
        var task = (Task)serviceType.GetMethod("CheckAsync", BindingFlags.Public | BindingFlags.Instance)!
            .Invoke(service, [CancellationToken.None])!;
        task.GetAwaiter().GetResult();
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var resultType = result.GetType();
        var updateAvailable = (bool)resultType.GetProperty("UpdateAvailable")!.GetValue(result)!;
        var latestVersion = (Version)resultType.GetProperty("LatestVersion")!.GetValue(result)!;
        var installerName = (string)resultType.GetProperty("InstallerName")!.GetValue(result)!;
        var releaseUrl = (string)resultType.GetProperty("ReleaseUrl")!.GetValue(result)!;
        if (!updateAvailable || latestVersion != expectedTarget || installerName != "HuahaiClipboard-Setup.exe" ||
            !Uri.TryCreate(releaseUrl, UriKind.Absolute, out var releaseUri) || releaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException($"Released update component did not discover {expectedTarget}.");
        }

        return new
        {
            status = "passed",
            currentVersion = currentVersion.ToString(3),
            latestVersion = latestVersion.ToString(3),
            updateAvailable,
            installerName,
            releaseUrl,
            source = fixtureClient is null ? "live-release" : "local-fixture"
        };
    }
    finally
    {
        fixtureClient?.Dispose();
    }
}

static void ExtractInstaller(string installerPath, string destination)
{
    if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true);
    Directory.CreateDirectory(destination);
    var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    var loadContext = new AssemblyLoadContext($"installer-payload-{Guid.NewGuid():N}", isCollectible: true);
    try
    {
        var assembly = loadContext.LoadFromAssemblyPath(installerPath);
        using var resource = assembly.GetManifestResourceStream(PayloadResource)
            ?? throw new InvalidDataException($"Installer is missing {PayloadResource}.");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Installer payload escapes its destination: {entry.FullName}");
            }
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: false);
        }
    }
    finally
    {
        loadContext.Unload();
    }
    if (!RequiredPayloadExists(destination))
    {
        throw new InvalidDataException("Installer payload is incomplete.");
    }
}

static bool RequiredPayloadExists(string root) =>
    File.Exists(Path.Combine(root, "HuahaiClipboard.App.exe")) &&
    File.Exists(Path.Combine(root, "HuahaiClipboard.Core.dll")) &&
    File.Exists(Path.Combine(root, "Assets", "Web", "product-shell.html"));

static string ApplicationVersion(string root)
{
    var version = FileVersionInfo.GetVersionInfo(Path.Combine(root, "HuahaiClipboard.App.exe")).FileVersion;
    return string.IsNullOrWhiteSpace(version) ? throw new InvalidDataException("Application version is missing.") : version;
}

static void RequireVersion(string root, string expected)
{
    var actual = ApplicationVersion(root);
    if (!Version.TryParse(actual, out var actualVersion) || !Version.TryParse(expected, out var expectedVersion) ||
        actualVersion.Major != expectedVersion.Major || actualVersion.Minor != expectedVersion.Minor || actualVersion.Build != expectedVersion.Build)
    {
        throw new InvalidDataException($"Expected application version {expected}, found {actual}.");
    }
}

static string Sha256(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static void MoveDirectoryWithRetry(string source, string destination)
{
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            Directory.Move(source, destination);
            return;
        }
        catch (IOException) when (attempt < 74)
        {
            Thread.Sleep(200);
        }
        catch (UnauthorizedAccessException) when (attempt < 74)
        {
            Thread.Sleep(200);
        }
    }
}

static Dictionary<string, string> ParseOptions(string[] values)
{
    if (values.Length % 2 != 0) throw new ArgumentException("Probe options must use --name value pairs.");
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < values.Length; index += 2)
    {
        if (!values[index].StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(values[index + 1]))
        {
            throw new ArgumentException("Probe options must use --name value pairs.");
        }
        result.Add(values[index][2..], values[index + 1]);
    }
    return result;
}

static string Required(IReadOnlyDictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing --{name}.");

static string RequiredPath(IReadOnlyDictionary<string, string> options, string name, bool mustExist)
{
    var path = Path.GetFullPath(Required(options, name));
    if (mustExist && !File.Exists(path)) throw new FileNotFoundException($"Missing --{name} file.", path);
    return path;
}

sealed class StaticReleaseHandler(string payload) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
}
