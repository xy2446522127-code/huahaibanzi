namespace HuahaiClipboard.Core.Todo;

public sealed class TodoWorkspaceService(JsonTodoWorkspaceStore store, TodoNoteImageRewriter? imageRewriter = null)
{
    private readonly JsonTodoWorkspaceStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TodoNoteImageRewriter? imageRewriter = imageRewriter;
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<TodoWorkspace> LoadAsync(CancellationToken cancellationToken = default) =>
        store.LoadAsync(cancellationToken);

    public Task<TodoItem> AddTodoAsync(string text, CancellationToken cancellationToken = default) =>
        UpdateAsync(workspace =>
        {
            var todo = new TodoItem(Guid.NewGuid().ToString("N"), NormalizeTodoText(text), false, workspace.Todos.Count + 1);
            return (workspace with { Todos = [.. workspace.Todos, todo] }, todo);
        }, cancellationToken);

    public Task ToggleTodoAsync(string id, CancellationToken cancellationToken = default) =>
        UpdateOnlyAsync(workspace => workspace with
        {
            Todos = workspace.Todos.Select(todo => todo.Id == id ? todo with { IsCompleted = !todo.IsCompleted } : todo).ToArray()
        }, cancellationToken);

    public Task DeleteTodoAsync(string id, CancellationToken cancellationToken = default) =>
        UpdateOnlyAsync(workspace => workspace with { Todos = workspace.Todos.Where(todo => todo.Id != id).ToArray() }, cancellationToken);

    public Task MoveTodoAsync(string id, string targetId, bool insertAfterTarget, CancellationToken cancellationToken = default) =>
        UpdateOnlyAsync(workspace =>
        {
            if (id == targetId) return workspace;
            var source = workspace.Todos.SingleOrDefault(todo => todo.Id == id);
            var target = workspace.Todos.SingleOrDefault(todo => todo.Id == targetId);
            if (source is null || target is null) return workspace;
            var todos = workspace.Todos.Where(todo => todo.Id != id).ToList();
            var index = todos.FindIndex(todo => todo.Id == targetId);
            todos.Insert(index + (insertAfterTarget ? 1 : 0), source);
            return workspace with
            {
                Todos = todos.Select((todo, index) => todo with { SortOrder = index + 1 }).ToArray()
            };
        }, cancellationToken);

    public Task<TodoNote> AddNoteAsync(string title, CancellationToken cancellationToken = default) =>
        UpdateAsync(workspace =>
        {
            var note = new TodoNote(Guid.NewGuid().ToString("N"), NormalizeTitle(title), string.Empty);
            return (workspace with { Notes = [note, .. workspace.Notes] }, note);
        }, cancellationToken);

    public async Task UpdateNoteAsync(string id, string title, string html, CancellationToken cancellationToken = default)
    {
        var persistedHtml = imageRewriter is null
            ? html ?? string.Empty
            : await imageRewriter.PersistAsync(html ?? string.Empty, cancellationToken);
        await UpdateOnlyAsync(workspace => workspace with
        {
            Notes = workspace.Notes.Select(note => note.Id == id
                ? note with { Title = NormalizeTitle(title), Html = persistedHtml }
                : note).ToArray()
        }, cancellationToken);
    }

    public async Task<TodoWorkspace> LoadForDisplayAsync(CancellationToken cancellationToken = default)
    {
        var workspace = await store.LoadAsync(cancellationToken);
        if (imageRewriter is null) return workspace;
        var notes = new List<TodoNote>(workspace.Notes.Count);
        foreach (var note in workspace.Notes)
        {
            notes.Add(note with { Html = await imageRewriter.HydrateAsync(note.Html, cancellationToken) });
        }
        return workspace with { Notes = notes };
    }

    public Task DeleteNoteAsync(string id, CancellationToken cancellationToken = default) =>
        UpdateOnlyAsync(workspace => workspace with { Notes = workspace.Notes.Where(note => note.Id != id).ToArray() }, cancellationToken);

    public Task SetCapsuleEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        UpdateOnlyAsync(workspace => workspace with { CapsuleEnabled = enabled }, cancellationToken);

    private async Task<T> UpdateAsync<T>(Func<TodoWorkspace, (TodoWorkspace Workspace, T Result)> update, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await store.LoadAsync(cancellationToken);
            var result = update(current);
            await store.SaveAsync(result.Workspace, cancellationToken);
            return result.Result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task UpdateOnlyAsync(Func<TodoWorkspace, TodoWorkspace> update, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await store.SaveAsync(update(await store.LoadAsync(cancellationToken)), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string NormalizeTodoText(string text) => string.IsNullOrWhiteSpace(text) ? "新待办" : text.Trim();

    private static string NormalizeTitle(string title) => string.IsNullOrWhiteSpace(title) ? "无标题笔记" : title.Trim();
}
