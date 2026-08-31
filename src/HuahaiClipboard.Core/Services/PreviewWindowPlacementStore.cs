using System.Text.Json;

namespace HuahaiClipboard.Core.Services;

public sealed record PreviewWindowPlacement(
    string DisplayId,
    int X,
    int Y,
    int Width,
    int Height,
    bool Topmost);

public sealed record PreviewWorkArea(int X, int Y, int Width, int Height);

public sealed class PreviewWindowPlacementStore
{
    public const int DefaultWidth = 650;
    public const int DefaultHeight = 500;
    public const int MinimumWidth = 420;
    public const int MinimumHeight = 360;
    public const int WorkAreaMargin = 16;

    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public PreviewWindowPlacementStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    public async Task<PreviewWindowPlacement?> LoadLastAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return JsonSerializer.Deserialize<PreviewWindowPlacement>(json);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(PreviewWindowPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentException.ThrowIfNullOrWhiteSpace(placement.DisplayId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = filePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(placement), cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public static PreviewWindowPlacement Clamp(
        PreviewWindowPlacement? placement,
        string displayId,
        PreviewWorkArea workArea)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayId);
        ArgumentNullException.ThrowIfNull(workArea);
        if (workArea.Width <= WorkAreaMargin * 2 || workArea.Height <= WorkAreaMargin * 2)
        {
            throw new ArgumentOutOfRangeException(nameof(workArea));
        }

        var maximumWidth = workArea.Width - WorkAreaMargin * 2;
        var maximumHeight = workArea.Height - WorkAreaMargin * 2;
        var width = Math.Clamp(placement?.Width ?? DefaultWidth, MinimumWidth, maximumWidth);
        var height = Math.Clamp(placement?.Height ?? DefaultHeight, MinimumHeight, maximumHeight);
        var minimumX = workArea.X + WorkAreaMargin;
        var minimumY = workArea.Y + WorkAreaMargin;
        var maximumX = workArea.X + workArea.Width - WorkAreaMargin - width;
        var maximumY = workArea.Y + workArea.Height - WorkAreaMargin - height;
        var x = placement is null ? workArea.X + (workArea.Width - width) / 2 : Math.Clamp(placement.X, minimumX, maximumX);
        var y = placement is null ? workArea.Y + (workArea.Height - height) / 2 : Math.Clamp(placement.Y, minimumY, maximumY);
        return new PreviewWindowPlacement(displayId, x, y, width, height, placement?.Topmost ?? true);
    }
}
