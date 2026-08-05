using System.Collections.ObjectModel;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Settings;
using HuahaiClipboard.NativeUiSpike.Models;

namespace HuahaiClipboard.NativeUiSpike.Presentation;

public sealed class NativeUiSpikeViewModel : ObservableObject
{
    private static readonly HashSet<string> ThemeIds =
    [
        "rose-purple",
        "cobalt-blue",
        "emerald-cyan",
        "amber-orange",
        "aurora-cyan-purple",
    ];

    private bool isSettingsOpen;
    private ShellSettings currentSettings = ShellSettings.Default;
    private readonly IClipboardHistorySource? historySource;
    private readonly IPanelActionSink? actionSink;
    private readonly ISettingsStore? settingsStore;
    private int retentionDays = 7;
    private string searchText = string.Empty;
    private ClipboardFilter selectedFilter = ClipboardFilter.All;
    private string themeId = "rose-purple";
    private IReadOnlyList<SpikeClipboardItem> visibleItems = [];

    private NativeUiSpikeViewModel(
        IEnumerable<SpikeClipboardItem> items,
        IClipboardHistorySource? historySource = null,
        IPanelActionSink? actionSink = null,
        ISettingsStore? settingsStore = null)
    {
        this.historySource = historySource;
        this.actionSink = actionSink;
        this.settingsStore = settingsStore;
        AllItems = new ObservableCollection<SpikeClipboardItem>(items);
        RefreshVisibleItems();
    }

    public ObservableCollection<SpikeClipboardItem> AllItems { get; }

    public ShellSettings CurrentSettings
    {
        get => currentSettings;
        private set => SetProperty(ref currentSettings, value);
    }

    public event EventHandler<ShellSettings>? SettingsChanged;

    public IReadOnlyList<SpikeClipboardItem> VisibleItems
    {
        get => visibleItems;
        private set => SetProperty(ref visibleItems, value);
    }

    public string HeaderSummary => $"最近 {RetentionDays} 天 · {VisibleItems.Count} 条";

    public ClipboardFilter SelectedFilter
    {
        get => selectedFilter;
        private set
        {
            if (SetProperty(ref selectedFilter, value)) RefreshVisibleItems();
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty)) RefreshVisibleItems();
        }
    }

    public string ThemeId
    {
        get => themeId;
        private set => SetProperty(ref themeId, value);
    }

    public bool IsSettingsOpen
    {
        get => isSettingsOpen;
        private set => SetProperty(ref isSettingsOpen, value);
    }

    public int RetentionDays
    {
        get => retentionDays;
        private set => SetProperty(ref retentionDays, value);
    }

    public static NativeUiSpikeViewModel CreateFixture(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return new NativeUiSpikeViewModel(Enumerable.Range(1, count).Select(CreateFixtureItem));
    }

    public static NativeUiSpikeViewModel CreateProduction(
        IClipboardHistorySource historySource,
        IPanelActionSink actionSink,
        ISettingsStore settingsStore)
    {
        ArgumentNullException.ThrowIfNull(historySource);
        ArgumentNullException.ThrowIfNull(actionSink);
        ArgumentNullException.ThrowIfNull(settingsStore);
        return new NativeUiSpikeViewModel([], historySource, actionSink, settingsStore);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (historySource is null || settingsStore is null) return;
        var settings = await settingsStore.LoadAsync(cancellationToken);
        CurrentSettings = settings;
        retentionDays = settings.Behavior.AutoCleanupDays;
        themeId = settings.Appearance.ThemeId;
        await ReloadHistoryAsync(cancellationToken);
        OnPropertyChanged(nameof(RetentionDays));
        OnPropertyChanged(nameof(ThemeId));
        OnPropertyChanged(nameof(HeaderSummary));
    }

    public Task UpdateAppearanceAsync(
        string theme,
        double opacity,
        double panelScale,
        CancellationToken cancellationToken = default) =>
        SaveSettingsAsync(
            CurrentSettings with
            {
                Appearance = CurrentSettings.Appearance with
                {
                    ThemeId = ThemeIds.Contains(theme) ? theme : CurrentSettings.Appearance.ThemeId,
                    Opacity = Math.Clamp(opacity, 0.65, 0.96),
                    PanelScale = Math.Clamp(panelScale, 0.8, 1.6),
                },
            },
            cancellationToken);

    public Task UpdateMotionAsync(
        PetalLevel petalLevel,
        bool reduceMotion,
        int clickDurationMs,
        CancellationToken cancellationToken = default) =>
        SaveSettingsAsync(
            CurrentSettings with
            {
                Motion = CurrentSettings.Motion with
                {
                    PetalLevel = petalLevel,
                    ReduceMotion = reduceMotion,
                    ClickDurationMs = Math.Clamp(clickDurationMs, 180, 900),
                },
            },
            cancellationToken);

    public Task UpdateInputAsync(
        bool rightDoubleClickEnabled,
        bool hotkeyEnabled,
        string[] excludedApplications,
        string? customShortcut,
        CancellationToken cancellationToken = default) =>
        SaveSettingsAsync(
            CurrentSettings with
            {
                Input = new InputSettings(
                    rightDoubleClickEnabled,
                    hotkeyEnabled,
                    excludedApplications
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    string.IsNullOrWhiteSpace(customShortcut) ? null : customShortcut.Trim()),
            },
            cancellationToken);

    public Task UpdateBehaviorAsync(
        bool backgroundEnabled,
        int autoCleanupDays,
        bool checkUpdatesOnStartup,
        CancellationToken cancellationToken = default) =>
        SaveSettingsAsync(
            CurrentSettings with
            {
                Behavior = new BehaviorSettings(
                    backgroundEnabled,
                    autoCleanupDays is 3 or 7 or 30 ? autoCleanupDays : 7,
                    checkUpdatesOnStartup),
            },
            cancellationToken);

    public async Task ReloadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (historySource is null) return;
        var records = await historySource.GetAllAsync(cancellationToken);
        AllItems.Clear();
        foreach (var record in records) AllItems.Add(SpikeClipboardItem.FromRecord(record));
        RefreshVisibleItems();
    }

    public async Task<bool> CopyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (actionSink is null) return AllItems.Any(item => item.Id == id);
        var result = await actionSink.CopyAsync(id, cancellationToken);
        return result.Succeeded;
    }

    public async Task TogglePinnedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = AllItems.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null) return;
        if (historySource is not null) await historySource.SetPinnedAsync(id, !item.IsPinned, cancellationToken);
        item.IsPinned = !item.IsPinned;
        RefreshVisibleItems();
    }

    public async Task ToggleFavoriteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = AllItems.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null) return;
        if (historySource is not null) await historySource.SetFavoriteAsync(id, !item.IsFavorite, cancellationToken);
        item.IsFavorite = !item.IsFavorite;
        RefreshVisibleItems();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (historySource is not null) await historySource.DeleteAsync(id, cancellationToken);
        Delete(id);
    }

    public async Task<int> ClearOrdinaryAsync(CancellationToken cancellationToken = default)
    {
        if (historySource is null) return ClearOrdinary();
        var before = AllItems.Count;
        await historySource.ClearUnprotectedAsync(cancellationToken);
        await ReloadHistoryAsync(cancellationToken);
        return before - AllItems.Count;
    }

    public async Task<int> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        if (historySource is null) return ClearAll();
        var before = AllItems.Count;
        await historySource.ClearAsync(cancellationToken);
        await ReloadHistoryAsync(cancellationToken);
        return before;
    }

    public void TogglePinned(Guid id)
    {
        var item = AllItems.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null) return;
        item.IsPinned = !item.IsPinned;
        RefreshVisibleItems();
    }

    public void ToggleFavorite(Guid id)
    {
        var item = AllItems.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null) return;
        item.IsFavorite = !item.IsFavorite;
        RefreshVisibleItems();
    }

    public void Delete(Guid id)
    {
        var item = AllItems.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null) return;
        AllItems.Remove(item);
        RefreshVisibleItems();
    }

    public int ClearOrdinary()
    {
        var removable = AllItems.Where(item => !item.IsPinned && !item.IsFavorite).ToArray();
        foreach (var item in removable) AllItems.Remove(item);
        RefreshVisibleItems();
        return removable.Length;
    }

    public int ClearAll()
    {
        var removed = AllItems.Count;
        AllItems.Clear();
        RefreshVisibleItems();
        return removed;
    }

    public void SelectFilter(ClipboardFilter filter) => SelectedFilter = filter;

    public bool SetTheme(string id)
    {
        if (!ThemeIds.Contains(id)) return false;
        ThemeId = id;
        return true;
    }

    public bool SetRetentionDays(int days)
    {
        if (days is not (3 or 7 or 30)) return false;
        if (RetentionDays == days) return true;
        RetentionDays = days;
        OnPropertyChanged(nameof(HeaderSummary));
        return true;
    }

    public void OpenSettings(bool open) => IsSettingsOpen = open;

    private async Task SaveSettingsAsync(ShellSettings settings, CancellationToken cancellationToken)
    {
        if (settingsStore is not null) await settingsStore.SaveAsync(settings, cancellationToken);
        CurrentSettings = settings;
        retentionDays = settings.Behavior.AutoCleanupDays;
        themeId = settings.Appearance.ThemeId;
        OnPropertyChanged(nameof(RetentionDays));
        OnPropertyChanged(nameof(ThemeId));
        OnPropertyChanged(nameof(HeaderSummary));
        SettingsChanged?.Invoke(this, settings);
    }

    private static SpikeClipboardItem CreateFixtureItem(int number)
    {
        var kind = (ClipboardItemKind)((number - 1) % 4);
        var suffix = number.ToString("0000");
        var (title, metadata) = kind switch
        {
            ClipboardItemKind.Text => ($"示例文本 {suffix}", "刚刚 · 28 个字符"),
            ClipboardItemKind.Link => ($"示例链接 {suffix}", $"https://huahai.example/{suffix}"),
            ClipboardItemKind.Image => ($"花海图片_{suffix}.png", "PNG 图片 · 1280 × 720"),
            ClipboardItemKind.File => ($"设计资料_{suffix}.pdf", "PDF 文件 · 2.4 MB"),
            _ => throw new InvalidOperationException($"Unsupported fixture kind: {kind}"),
        };

        return new SpikeClipboardItem(
            Guid.Parse($"00000000-0000-0000-0000-{number:000000000000}"),
            $"fixture-{suffix}",
            kind,
            title,
            metadata);
    }

    private void RefreshVisibleItems()
    {
        IEnumerable<SpikeClipboardItem> query = AllItems;
        query = SelectedFilter switch
        {
            ClipboardFilter.Text => query.Where(item => item.Kind == ClipboardItemKind.Text),
            ClipboardFilter.Link => query.Where(item => item.Kind == ClipboardItemKind.Link),
            ClipboardFilter.Image => query.Where(item => item.Kind == ClipboardItemKind.Image),
            ClipboardFilter.File => query.Where(item => item.Kind == ClipboardItemKind.File),
            ClipboardFilter.Favorites => query.Where(item => item.IsFavorite),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(item =>
                item.Title.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                item.Metadata.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase));
        }

        VisibleItems = query
            .OrderByDescending(item => item.IsPinned)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ToArray();
        OnPropertyChanged(nameof(HeaderSummary));
    }
}
