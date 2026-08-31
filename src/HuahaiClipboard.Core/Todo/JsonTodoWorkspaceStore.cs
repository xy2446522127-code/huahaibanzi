using System.Text.Json;

namespace HuahaiClipboard.Core.Todo;

public sealed class JsonTodoWorkspaceStore
{
    private readonly string filePath;
    private readonly SemaphoreSlim gate = new(1, 1);

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

            var temporaryPath = filePath + ".tmp";
            var json = JsonSerializer.Serialize(workspace.Normalize());
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }
}
