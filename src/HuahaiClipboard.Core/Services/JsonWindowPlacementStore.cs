using System.Text.Json;

namespace HuahaiClipboard.Core.Services;

public sealed record WindowPlacement(string DisplayId, int X, int Y);

public sealed class JsonWindowPlacementStore
{
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public JsonWindowPlacementStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    public async Task<WindowPlacement?> LoadAsync(string displayId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            return (await LoadStateAsync(cancellationToken)).Positions.GetValueOrDefault(displayId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<WindowPlacement?> LoadLastAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            return state.LastDisplayId is null ? null : state.Positions.GetValueOrDefault(state.LastDisplayId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(WindowPlacement placement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentException.ThrowIfNullOrWhiteSpace(placement.DisplayId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            state.Positions[placement.DisplayId] = placement;
            state.LastDisplayId = placement.DisplayId;
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = filePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(state), cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<WindowPlacementState> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath)) return new WindowPlacementState();
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<WindowPlacementState>(json) ?? new WindowPlacementState();
        }
        catch (JsonException)
        {
            return new WindowPlacementState();
        }
        catch (IOException)
        {
            return new WindowPlacementState();
        }
    }

    private sealed class WindowPlacementState
    {
        public string? LastDisplayId { get; set; }
        public Dictionary<string, WindowPlacement> Positions { get; set; } = new(StringComparer.Ordinal);
    }
}
