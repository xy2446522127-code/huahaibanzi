using System.Text.Json;

namespace HuahaiClipboard.Core.Services;

public static class StaticUpdateManifest
{
    public static bool TryCreateUpdate(
        string json,
        Version currentVersion,
        out UpdateCheckResult? update)
    {
        update = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var versionText = GetString(root, "version");
            var releaseUrl = GetString(root, "releaseUrl");
            var installerUrl = GetString(root, "installerUrl");
            var sha256 = GetString(root, "sha256");
            var size = root.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var value)
                ? value
                : 0;
            if (!Version.TryParse(versionText, out var version) ||
                version <= currentVersion ||
                !Uri.TryCreate(releaseUrl, UriKind.Absolute, out var releaseUri) ||
                !Uri.TryCreate(installerUrl, UriKind.Absolute, out var installerUri) ||
                releaseUri.Scheme != Uri.UriSchemeHttps ||
                installerUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(releaseUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(installerUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !installerUri.AbsolutePath.EndsWith(
                    "/" + GitHubUpdateCheckService.InstallerAssetName,
                    StringComparison.OrdinalIgnoreCase) ||
                size <= 0 ||
                sha256 is not { Length: 64 } ||
                !sha256.All(Uri.IsHexDigit))
            {
                return false;
            }

            update = new UpdateCheckResult(
                true,
                currentVersion,
                version,
                releaseUri.AbsoluteUri,
                GitHubUpdateCheckService.InstallerAssetName,
                installerUri.AbsoluteUri,
                size,
                sha256.ToLowerInvariant());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
