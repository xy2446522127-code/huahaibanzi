using System.Text.Json;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.Core.Services;

public sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly AtomicJsonFileStore atomicFileStore = new();

    public async Task<ShellSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                return ShellSettings.Default;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var settings = Normalize(JsonSerializer.Deserialize<ShellSettings>(json));
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("Input", out var input) &&
                    !input.TryGetProperty("RightDoubleClickEnabled", out _) &&
                    input.TryGetProperty("MiddleClickEnabled", out var legacyMiddleClick) &&
                    legacyMiddleClick.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    settings = settings with
                    {
                        Input = settings.Input with
                        {
                            RightDoubleClickEnabled = legacyMiddleClick.GetBoolean()
                        }
                    };
                }

                return settings;
            }
            catch (JsonException)
            {
                return ShellSettings.Default;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(ShellSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await atomicFileStore.WriteVerifiedAsync(
                filePath,
                settings,
                value => JsonSerializer.Serialize(value),
                json => Normalize(JsonSerializer.Deserialize<ShellSettings>(json)),
                value => _ = value.Appearance,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static ShellSettings Normalize(ShellSettings? settings)
    {
        if (settings is null)
        {
            return ShellSettings.Default;
        }

        return settings with
        {
            Behavior = NormalizeBehavior(settings.Behavior),
            Input = settings.Input is null
                ? ShellSettings.Default.Input
                : settings.Input with
                {
                    ExcludedApplications = settings.Input.ExcludedApplications ?? []
                }
        };
    }

    private static BehaviorSettings NormalizeBehavior(BehaviorSettings? behavior)
    {
        if (behavior is null)
        {
            return BehaviorSettings.Default;
        }

        return behavior with
        {
            AutoCleanupDays = behavior.AutoCleanupDays is 3 or 7 or 30
                ? behavior.AutoCleanupDays
                : BehaviorSettings.Default.AutoCleanupDays,
            AutoCleanupCount = behavior.AutoCleanupCount is >= 1 and <= 10000
                ? behavior.AutoCleanupCount
                : BehaviorSettings.Default.AutoCleanupCount
        };
    }
}
