using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Settings;
using HuahaiClipboard.Core.Visual;

namespace HuahaiClipboard.Core.Presentation;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore settingsStore;
    private ShellSettings draft = ShellSettings.Default;
    private string saveStatus = "未修改";

    public SettingsViewModel(ISettingsStore settingsStore)
    {
        this.settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
    }

    public event EventHandler<ShellSettings>? PreviewChanged;

    public ShellSettings Draft
    {
        get => draft;
        private set => SetProperty(ref draft, value);
    }

    public IReadOnlyList<ThemeDefinition> Themes => ThemeCatalog.All;

    public IReadOnlyList<PetalLevel> PetalLevels { get; } = Enum.GetValues<PetalLevel>();

    public string SaveStatus
    {
        get => saveStatus;
        private set => SetProperty(ref saveStatus, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Draft = await settingsStore.LoadAsync(cancellationToken);
        SaveStatus = "已加载";
    }

    public Task UpdateAppearanceAsync(
        AppearanceSettings appearance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        return SaveDraftAsync(Draft with { Appearance = appearance }, cancellationToken);
    }

    public Task UpdateMotionAsync(
        MotionSettings motion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(motion);
        return SaveDraftAsync(Draft with { Motion = motion }, cancellationToken);
    }

    public Task UpdateInputAsync(
        InputSettings input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return SaveDraftAsync(Draft with { Input = input }, cancellationToken);
    }

    public Task ResetAppearanceAsync(CancellationToken cancellationToken = default) =>
        SaveDraftAsync(Draft with { Appearance = ShellSettings.Default.Appearance }, cancellationToken);

    private async Task SaveDraftAsync(
        ShellSettings settings,
        CancellationToken cancellationToken)
    {
        Draft = settings;
        SaveStatus = "正在保存";
        try
        {
            await settingsStore.SaveAsync(settings, cancellationToken);
            SaveStatus = "已保存";
            PreviewChanged?.Invoke(this, settings);
        }
        catch
        {
            SaveStatus = "保存失败";
            throw;
        }
    }
}
