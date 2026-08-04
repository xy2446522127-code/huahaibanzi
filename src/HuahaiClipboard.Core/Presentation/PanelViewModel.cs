using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Presentation;

public sealed class PanelViewModel : ObservableObject
{
    private readonly IClipboardHistorySource historySource;
    private readonly IPanelActionSink actionSink;
    private readonly IWindowNavigator navigator;
    private IReadOnlyList<ClipboardRecord> allRecords = Array.Empty<ClipboardRecord>();
    private IReadOnlyList<ClipboardRecord> visibleRecords = Array.Empty<ClipboardRecord>();
    private string searchText = string.Empty;
    private ClipboardFilter selectedFilter;
    private ClipboardRecord? selectedRecord;
    private bool isLoading;
    private bool isEmpty = true;
    private bool isBusy;
    private string? recoveryMessage;

    public PanelViewModel(
        IClipboardHistorySource historySource,
        IPanelActionSink actionSink,
        IWindowNavigator navigator)
    {
        this.historySource = historySource ?? throw new ArgumentNullException(nameof(historySource));
        this.actionSink = actionSink ?? throw new ArgumentNullException(nameof(actionSink));
        this.navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
    }

    public IReadOnlyList<ClipboardRecord> AllRecords
    {
        get => allRecords;
        private set => SetProperty(ref allRecords, value);
    }

    public IReadOnlyList<ClipboardRecord> VisibleRecords
    {
        get => visibleRecords;
        private set => SetProperty(ref visibleRecords, value);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty))
            {
                RefreshVisibleRecords();
            }
        }
    }

    public ClipboardFilter SelectedFilter
    {
        get => selectedFilter;
        set
        {
            if (SetProperty(ref selectedFilter, value))
            {
                RefreshVisibleRecords();
            }
        }
    }

    public ClipboardRecord? SelectedRecord
    {
        get => selectedRecord;
        private set => SetProperty(ref selectedRecord, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public bool IsEmpty
    {
        get => isEmpty;
        private set => SetProperty(ref isEmpty, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

    public string? RecoveryMessage
    {
        get => recoveryMessage;
        private set => SetProperty(ref recoveryMessage, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            await RefreshAllRecordsAsync(cancellationToken);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task CopyAsync(
        ClipboardRecord record,
        CancellationToken cancellationToken = default) =>
        RunActionAsync(record, actionSink.CopyAsync, cancellationToken);

    public Task PasteAsync(
        ClipboardRecord record,
        CancellationToken cancellationToken = default) =>
        RunActionAsync(record, actionSink.PasteAsync, cancellationToken);

    public Task ToggleFavoriteAsync(
        ClipboardRecord record,
        CancellationToken cancellationToken = default) =>
        RunRecordMutationAsync(
            record,
            () => historySource.SetFavoriteAsync(record.Id, !record.IsFavorite, cancellationToken),
            value => value with { IsFavorite = !value.IsFavorite });

    public Task TogglePinnedAsync(
        ClipboardRecord record,
        CancellationToken cancellationToken = default) =>
        RunRecordMutationAsync(
            record,
            () => historySource.SetPinnedAsync(record.Id, !record.IsPinned, cancellationToken),
            value => value with { IsPinned = !value.IsPinned });

    public Task DeleteAsync(
        ClipboardRecord record,
        CancellationToken cancellationToken = default) =>
        RunDeleteAsync(record, cancellationToken);

    public void MoveSelection(int delta)
    {
        if (VisibleRecords.Count == 0)
        {
            SelectedRecord = null;
            return;
        }

        var currentIndex = SelectedRecord is null
            ? -1
            : IndexOf(VisibleRecords, SelectedRecord.Id);
        var targetIndex = Math.Clamp(currentIndex + delta, 0, VisibleRecords.Count - 1);
        SelectedRecord = VisibleRecords[targetIndex];
    }

    public void SelectRecord(ClipboardRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        SelectedRecord = VisibleRecords.FirstOrDefault(value => value.Id == record.Id);
    }

    public void Close() => navigator.HideTransientPanel();

    private async Task RunActionAsync(
        ClipboardRecord record,
        Func<Guid, CancellationToken, Task<PanelActionResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        RecoveryMessage = null;
        try
        {
            var result = await action(record.Id, cancellationToken);
            if (result.Succeeded)
            {
                navigator.HideTransientPanel();
            }
            else
            {
                RecoveryMessage = result.RecoveryMessage;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunRecordMutationAsync(
        ClipboardRecord record,
        Func<Task> mutation,
        Func<ClipboardRecord, ClipboardRecord> update)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        RecoveryMessage = null;
        try
        {
            await mutation();
            AllRecords = AllRecords
                .Select(value => value.Id == record.Id ? update(value) : value)
                .ToArray();
            RefreshVisibleRecords();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunDeleteAsync(ClipboardRecord record, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        RecoveryMessage = null;
        try
        {
            await historySource.DeleteAsync(record.Id, cancellationToken);
            AllRecords = AllRecords.Where(value => value.Id != record.Id).ToArray();
            RefreshVisibleRecords();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAllRecordsAsync(CancellationToken cancellationToken)
    {
        AllRecords = await historySource.GetAllAsync(cancellationToken);
        RefreshVisibleRecords();
    }

    private void RefreshVisibleRecords()
    {
        var selectedId = SelectedRecord?.Id;
        var query = SearchText.Trim();
        var filtered = AllRecords
            .Where(MatchesFilter)
            .Where(record => query.Length == 0 || MatchesSearch(record, query))
            .OrderByDescending(record => record.IsPinned)
            .ThenByDescending(record => record.LastCopiedAt)
            .ToArray();

        VisibleRecords = filtered;
        IsEmpty = filtered.Length == 0;
        SelectedRecord = selectedId is null
            ? filtered.FirstOrDefault()
            : filtered.FirstOrDefault(record => record.Id == selectedId) ?? filtered.FirstOrDefault();
    }

    private bool MatchesFilter(ClipboardRecord record) => SelectedFilter switch
    {
        ClipboardFilter.All => true,
        ClipboardFilter.Text => record.Kind == ClipboardItemKind.Text,
        ClipboardFilter.Link => record.Kind == ClipboardItemKind.Link,
        ClipboardFilter.Image => record.Kind == ClipboardItemKind.Image,
        ClipboardFilter.File => record.Kind == ClipboardItemKind.File,
        ClipboardFilter.Favorites => record.IsFavorite,
        _ => false
    };

    private static bool MatchesSearch(ClipboardRecord record, string query) =>
        record.PrimaryText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        record.SecondaryText.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static int IndexOf(IReadOnlyList<ClipboardRecord> records, Guid id)
    {
        for (var index = 0; index < records.Count; index++)
        {
            if (records[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }
}
