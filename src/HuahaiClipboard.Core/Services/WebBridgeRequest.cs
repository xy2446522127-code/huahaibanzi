using System.Text.Json;

namespace HuahaiClipboard.Core.Services;

public sealed record WebBridgeRequest(
    string Action,
    string? Id,
    string? Text,
    string[] Values,
    bool? Enabled,
    double? Number,
    double? X,
    double? Y,
    string? Mode)
{
    public static bool TryParse(string json, out WebBridgeRequest? request)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("action", out var actionElement) ||
                actionElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(actionElement.GetString()))
            {
                return false;
            }

            request = new WebBridgeRequest(
                actionElement.GetString()!,
                GetString(root, "id"),
                GetString(root, "text"),
                GetStrings(root, "values"),
                GetBoolean(root, "enabled"),
                GetNumber(root, "number"),
                GetNumber(root, "x"),
                GetNumber(root, "y"),
                GetString(root, "mode"));
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

    private static bool? GetBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static double? GetNumber(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)
            ? number
            : null;

    private static string[] GetStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }
}
