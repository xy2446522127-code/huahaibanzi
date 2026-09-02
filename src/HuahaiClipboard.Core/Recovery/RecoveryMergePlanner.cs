using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Todo;

namespace HuahaiClipboard.Core.Recovery;

public sealed record RecoveryDataset(
    IReadOnlyList<ClipboardRecord> History,
    TodoWorkspace Workspace);

public enum RecoveryConflictKind
{
    HistoryPayload,
    TodoText,
    NoteContent
}

public sealed record RecoveryConflict(RecoveryConflictKind Kind, string OriginalId, string RecoveredId);

public sealed record RecoveryPlan(
    IReadOnlyList<ClipboardRecord> History,
    TodoWorkspace Workspace,
    IReadOnlyList<RecoveryConflict> Conflicts);

public sealed class RecoveryMergePlanner
{
    public RecoveryPlan CreatePlan(RecoveryDataset source, RecoveryDataset destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        var conflicts = new List<RecoveryConflict>();
        var history = destination.History.ToList();
        MergeHistory(source.History, history, conflicts);
        var workspace = MergeWorkspace(source.Workspace, destination.Workspace, conflicts);
        return new RecoveryPlan(history, workspace, conflicts);
    }

    private static void MergeHistory(
        IEnumerable<ClipboardRecord> source,
        List<ClipboardRecord> destination,
        List<RecoveryConflict> conflicts)
    {
        foreach (var recovered in source)
        {
            var idIndex = destination.FindIndex(current => current.Id == recovered.Id);
            if (idIndex >= 0)
            {
                if (SamePayload(destination[idIndex], recovered))
                {
                    destination[idIndex] = MergeRecord(destination[idIndex], recovered);
                    continue;
                }

                var clone = recovered with { Id = Guid.NewGuid() };
                destination.Add(clone);
                conflicts.Add(new RecoveryConflict(RecoveryConflictKind.HistoryPayload, recovered.Id.ToString("N"), clone.Id.ToString("N")));
                continue;
            }

            var semanticIndex = destination.FindIndex(current => SamePayload(current, recovered));
            if (semanticIndex >= 0)
            {
                destination[semanticIndex] = MergeRecord(destination[semanticIndex], recovered);
            }
            else
            {
                destination.Add(recovered);
            }
        }
    }

    private static TodoWorkspace MergeWorkspace(
        TodoWorkspace source,
        TodoWorkspace destination,
        List<RecoveryConflict> conflicts)
    {
        var todos = destination.Todos.ToList();
        foreach (var recovered in source.Todos)
        {
            var existingIndex = todos.FindIndex(todo => todo.Id == recovered.Id);
            if (existingIndex < 0)
            {
                todos.Add(recovered);
                continue;
            }

            var current = todos[existingIndex];
            if (string.Equals(current.Text, recovered.Text, StringComparison.Ordinal))
            {
                todos[existingIndex] = current with { IsCompleted = current.IsCompleted || recovered.IsCompleted };
                continue;
            }

            var clone = recovered with { Id = Guid.NewGuid().ToString("N"), Text = recovered.Text + "（恢复副本）" };
            todos.Add(clone);
            conflicts.Add(new RecoveryConflict(RecoveryConflictKind.TodoText, recovered.Id, clone.Id));
        }

        var notes = destination.Notes.ToList();
        foreach (var recovered in source.Notes)
        {
            var existingIndex = notes.FindIndex(note => note.Id == recovered.Id);
            if (existingIndex < 0)
            {
                notes.Add(recovered);
                continue;
            }

            var current = notes[existingIndex];
            if (string.Equals(current.Title, recovered.Title, StringComparison.Ordinal) &&
                string.Equals(current.Html, recovered.Html, StringComparison.Ordinal))
            {
                continue;
            }

            var clone = recovered with
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = recovered.Title + "（恢复副本）"
            };
            notes.Add(clone);
            conflicts.Add(new RecoveryConflict(RecoveryConflictKind.NoteContent, recovered.Id, clone.Id));
        }

        return new TodoWorkspace(
            destination.CapsuleEnabled || source.CapsuleEnabled,
            todos,
            notes).Normalize();
    }

    private static bool SamePayload(ClipboardRecord first, ClipboardRecord second) =>
        first.Kind == second.Kind &&
        string.Equals(first.PrimaryText, second.PrimaryText, StringComparison.Ordinal) &&
        string.Equals(first.SourcePath, second.SourcePath, StringComparison.Ordinal);

    private static ClipboardRecord MergeRecord(ClipboardRecord current, ClipboardRecord recovered) =>
        current with
        {
            IsFavorite = current.IsFavorite || recovered.IsFavorite,
            IsPinned = current.IsPinned || recovered.IsPinned,
            IsAvailable = current.IsAvailable || recovered.IsAvailable,
            LastCopiedAt = current.LastCopiedAt >= recovered.LastCopiedAt
                ? current.LastCopiedAt
                : recovered.LastCopiedAt
        };
}
