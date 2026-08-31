namespace HuahaiClipboard.Core.Todo;

public sealed record TodoItem(string Id, string Text, bool IsCompleted, int SortOrder);

public sealed record TodoNote(string Id, string Title, string Html);

public sealed record TodoWorkspace(
    bool CapsuleEnabled,
    IReadOnlyList<TodoItem> Todos,
    IReadOnlyList<TodoNote> Notes)
{
    public static TodoWorkspace Empty { get; } = new(true, [], []);

    public TodoWorkspace Normalize()
    {
        var todos = (Todos ?? [])
            .Where(todo => !string.IsNullOrWhiteSpace(todo.Id))
            .OrderBy(todo => todo.SortOrder)
            .ThenBy(todo => todo.Id, StringComparer.Ordinal)
            .Select((todo, index) => todo with
            {
                Text = (todo.Text ?? string.Empty).Trim(),
                SortOrder = index + 1
            })
            .ToArray();
        var notes = (Notes ?? [])
            .Where(note => !string.IsNullOrWhiteSpace(note.Id))
            .Select(note => note with
            {
                Title = string.IsNullOrWhiteSpace(note.Title) ? "无标题笔记" : note.Title.Trim(),
                Html = note.Html ?? string.Empty
            })
            .ToArray();
        return new TodoWorkspace(CapsuleEnabled, todos, notes);
    }
}
