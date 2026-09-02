using System.Text.Json;
using HuahaiClipboard.Core.Services;

namespace HuahaiClipboard.Core.Todo;

public sealed class JsonTodoWorkspaceStore
{
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly AtomicJsonFileStore atomicFileStore = new();

    public JsonTodoWorkspaceStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    public async Task<TodoWorkspace> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(filePath))
            {
                return TodoWorkspace.Empty;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                return (JsonSerializer.Deserialize<TodoWorkspace>(json) ?? TodoWorkspace.Empty).Normalize();
            }
            catch (JsonException)
            {
                return TodoWorkspace.Empty;
            }
            catch (IOException)
            {
                return TodoWorkspace.Empty;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(TodoWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await atomicFileStore.WriteVerifiedAsync(
                filePath,
                workspace.Normalize(),
                value => JsonSerializer.Serialize(value),
                json => JsonSerializer.Deserialize<TodoWorkspace>(json) ?? throw new InvalidDataException("待办数据为空。"),
                value => _ = value.Normalize(),
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
