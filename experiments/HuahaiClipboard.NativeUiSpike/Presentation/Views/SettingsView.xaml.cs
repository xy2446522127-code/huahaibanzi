using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using HuahaiClipboard.Core.Settings;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.NativeUiSpike.Services;
using NativeGitHubUpdateCheckService = HuahaiClipboard.NativeUiSpike.Services.GitHubUpdateCheckService;

namespace HuahaiClipboard.NativeUiSpike.Presentation.Views;

public partial class SettingsView : UserControl
{
    private readonly DispatcherTimer clearAllTimer;
    private readonly DispatcherTimer appearanceSaveTimer;
    private readonly DispatcherTimer motionSaveTimer;
    private readonly DispatcherTimer toastTimer;
    private readonly NativeGitHubUpdateCheckService updateService = NativeGitHubUpdateCheckService.CreateDefault();
    private bool applyingSettings;
    private bool clearAllPending;
    private string? currentShortcut;
    private bool shortcutCaptureActive;

    public SettingsView()
    {
        InitializeComponent();
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.9) };
        toastTimer.Tick += (_, _) =>
        {
            toastTimer.Stop();
            ToastChrome.Visibility = Visibility.Collapsed;
        };
        clearAllTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        clearAllTimer.Tick += (_, _) => ResetClearAllConfirmation();
        appearanceSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        appearanceSaveTimer.Tick += async (_, _) =>
        {
            appearanceSaveTimer.Stop();
            await PersistAppearanceAsync();
        };
        motionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        motionSaveTimer.Tick += async (_, _) =>
        {
            motionSaveTimer.Stop();
            await PersistMotionAsync();
        };
        Loaded += (_, _) =>
        {
            SelectPage("Appearance", AppearanceNav);
            ApplyPersistedSettings();
            if (ViewModel?.CurrentSettings.Behavior.CheckUpdatesOnStartup == true)
            {
                _ = CheckForUpdatesAsync(showToast: false);
            }
        };
    }

    public event EventHandler? BackRequested;

    public event Action<double>? OpacityRequested;

    public event Action<double>? PanelScaleRequested;

    public event Action<bool>? PetalsChanged;

    public event Action<bool>? ReducedMotionChanged;

    public event Action<string>? ThemeRequested;

    public event Action<bool>? StartupChanged;

    private NativeUiSpikeViewModel? ViewModel => DataContext as NativeUiSpikeViewModel;

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page) SelectPage(page, button);
    }

    private void SelectPage(string page, Button activeButton)
    {
        var pages = new Dictionary<string, (Panel Panel, string Title, string Subtitle)>(StringComparer.Ordinal)
        {
            ["Appearance"] = (AppearancePage, "外观与主题", "更改会立即应用到当前面板。"),
            ["Motion"] = (MotionPage, "动效", "控制花瓣、液态反光和点击反馈。"),
            ["Input"] = (InputPage, "唤出与隐私", "设置默认和自定义唤出方式。"),
            ["Storage"] = (StoragePage, "本机存储", "查看缓存位置并管理历史记录。"),
            ["System"] = (SystemPage, "系统", "控制后台运行和开机启动。"),
            ["About"] = (AboutPage, "关于与更新", "查看版本并体验安全的更新提醒流程。"),
        };

        if (!pages.TryGetValue(page, out var selected)) return;
        foreach (var candidate in pages.Values) candidate.Panel.Visibility = Visibility.Collapsed;
        selected.Panel.Visibility = Visibility.Visible;
        PageTitle.Text = selected.Title;
        PageSubtitle.Text = selected.Subtitle;

        foreach (var button in FindVisualChildren<Button>(this).Where(button => button.Tag is string tag && pages.ContainsKey(tag)))
        {
            button.Background = button == activeButton
                ? new SolidColorBrush(Color.FromArgb(0x78, 0xD7, 0x86, 0xBB))
                : Brushes.Transparent;
            button.Foreground = button == activeButton ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xCA, 0xBC, 0xCA));
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string themeId || ViewModel?.SetTheme(themeId) != true) return;
        ThemeRequested?.Invoke(themeId);
        await PersistAppearanceAsync();
        HighlightTheme(button);
        ShowToast("主题已切换");
    }

    private void HighlightTheme(Button active)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal)
        {
            "rose-purple", "cobalt-blue", "emerald-cyan", "amber-orange", "aurora-cyan-purple",
        };
        foreach (var button in FindVisualChildren<Button>(AppearancePage).Where(button => button.Tag is string tag && ids.Contains(tag)))
        {
            button.BorderBrush = button == active ? (Brush)FindResource("HuahaiAccentBrush") : new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
            button.BorderThickness = button == active ? new Thickness(2) : new Thickness(1);
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValue is null) return;
        var percent = (int)Math.Round(e.NewValue);
        OpacityValue.Text = $"{percent}%";
        OpacityRequested?.Invoke(percent / 100d);
        if (!applyingSettings) RestartTimer(appearanceSaveTimer);
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValue is null) return;
        var percent = (int)Math.Round(e.NewValue / 5d) * 5;
        ScaleValue.Text = $"{percent}%";
        PanelScaleRequested?.Invoke(percent / 100d);
        if (!applyingSettings) RestartTimer(appearanceSaveTimer);
    }

    private void ResetScale_Click(object sender, RoutedEventArgs e)
    {
        ScaleSlider.Value = 100;
        ShowToast("面板缩放已恢复为 100%");
    }

    private void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DurationValue is null) return;
        DurationValue.Text = $"{(int)Math.Round(e.NewValue)}ms";
        if (!applyingSettings) RestartTimer(motionSaveTimer);
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle) return;
        var enabled = toggle.IsChecked == true;
        var name = toggle.Tag?.ToString() ?? "选项";
        ShowToast($"{name}：{(enabled ? "已开启" : "已关闭")}");

        if (applyingSettings || ViewModel is null) return;

        if (ReferenceEquals(toggle, PetalToggle))
        {
            PetalsChanged?.Invoke(enabled);
            await PersistMotionAsync();
        }
        else if (ReferenceEquals(toggle, ReducedMotionToggle))
        {
            ReducedMotionChanged?.Invoke(enabled);
            await PersistMotionAsync();
        }
        else if (ReferenceEquals(toggle, RightDoubleToggle))
        {
            await PersistInputAsync();
        }
        else if (ReferenceEquals(toggle, StartupToggle))
        {
            StartupChanged?.Invoke(enabled);
        }
        else if (ReferenceEquals(toggle, BackgroundToggle) || ReferenceEquals(toggle, UpdateToggle))
        {
            await PersistBehaviorAsync();
        }
    }

    private void CaptureShortcut_Click(object sender, RoutedEventArgs e)
    {
        shortcutCaptureActive = true;
        CaptureShortcutButton.Content = "正在监听… 请按键盘或鼠标按键";
        CaptureShortcutButton.Focus();
        ShowToast("请输入新的快捷唤出方式");
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!shortcutCaptureActive) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var gesture = FormatKeyboardGesture(Keyboard.Modifiers, key);
        if (gesture is null)
        {
            ShowToast("普通按键需要搭配 Ctrl、Alt、Shift 或 Win");
            return;
        }

        CompleteShortcutCapture(gesture);
        e.Handled = true;
    }

    private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!shortcutCaptureActive) return;
        var gesture = e.ChangedButton switch
        {
            MouseButton.Middle => "鼠标中键",
            MouseButton.XButton1 => "鼠标侧键 1",
            MouseButton.XButton2 => "鼠标侧键 2",
            _ => null,
        };
        if (gesture is null)
        {
            ShowToast("左键和右键用于正常操作，请使用中键或侧键");
            return;
        }

        CompleteShortcutCapture(gesture);
        e.Handled = true;
    }

    private void CompleteShortcutCapture(string gesture)
    {
        shortcutCaptureActive = false;
        currentShortcut = gesture;
        CaptureShortcutButton.Content = $"点击后自定义快捷唤出　　{gesture}";
        _ = PersistInputAsync();
        ShowToast($"已保存：{gesture}");
    }

    private async void ResetShortcut_Click(object sender, RoutedEventArgs e)
    {
        shortcutCaptureActive = false;
        currentShortcut = null;
        CaptureShortcutButton.Content = "点击后自定义快捷唤出　　未设置";
        await PersistInputAsync(hotkeyEnabled: false);
        ShowToast("已恢复默认右键双击唤出");
    }

    private async void SaveExclusions_Click(object sender, RoutedEventArgs e)
    {
        var count = ExclusionTextBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        await PersistInputAsync();
        ShowToast($"应用排除列表已保存（{count} 项）");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = DataPathText.Text;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private async void Retention_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var days) || ViewModel?.SetRetentionDays(days) != true) return;
        await PersistBehaviorAsync();
        ShowToast($"自动清理期限已设为 {(days == 30 ? "1 个月" : $"{days} 天")}");
    }

    private async void ClearOrdinary_Click(object sender, RoutedEventArgs e)
    {
        var removed = ViewModel is null ? 0 : await ViewModel.ClearOrdinaryAsync();
        ShowToast($"已清空 {removed} 条普通记录，收藏和置顶已保留");
    }

    private async void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (!clearAllPending)
        {
            clearAllPending = true;
            ClearAllButton.Content = "再次点击确认";
            clearAllTimer.Stop();
            clearAllTimer.Start();
            ShowToast("再次点击将删除全部历史记录");
            return;
        }

        var removed = ViewModel is null ? 0 : await ViewModel.ClearAllAsync();
        ResetClearAllConfirmation();
        ShowToast($"已清空全部 {removed} 条历史记录");
    }

    private void ResetClearAllConfirmation()
    {
        clearAllTimer.Stop();
        clearAllPending = false;
        ClearAllButton.Content = "清空全部";
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e) =>
        await CheckForUpdatesAsync(showToast: true);

    private void Release_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(NativeGitHubUpdateCheckService.ReleasesPage) { UseShellExecute = true });

    private async Task CheckForUpdatesAsync(bool showToast)
    {
        UpdateStatus.Text = "正在连接 GitHub 检查更新…";
        try
        {
            var result = await updateService.CheckAsync(CancellationToken.None);
            UpdateStatus.Text = result.UpdateAvailable
                ? $"发现新版本 {result.LatestVersion}，点击“查看 GitHub Release”下载。"
                : $"已是最新版本 {result.CurrentVersion}。";
            if (showToast) ShowToast(result.UpdateAvailable ? "发现可用更新" : "当前已是最新版");
        }
        catch (Exception exception)
        {
            UpdateStatus.Text = $"暂时无法检查更新：{exception.Message}";
            if (showToast) ShowToast("更新检查失败，请稍后重试");
        }
    }

    public void SetStartupState(bool enabled)
    {
        applyingSettings = true;
        StartupToggle.IsChecked = enabled;
        applyingSettings = false;
    }

    private void ApplyPersistedSettings()
    {
        if (ViewModel is null) return;
        var settings = ViewModel.CurrentSettings;
        applyingSettings = true;
        OpacitySlider.Value = settings.Appearance.Opacity * 100;
        ScaleSlider.Value = settings.Appearance.PanelScale * 100;
        PetalToggle.IsChecked = settings.Motion.PetalLevel != PetalLevel.Off;
        ReducedMotionToggle.IsChecked = settings.Motion.ReduceMotion;
        DurationSlider.Value = settings.Motion.ClickDurationMs;
        RightDoubleToggle.IsChecked = settings.Input.RightDoubleClickEnabled;
        BackgroundToggle.IsChecked = settings.Behavior.BackgroundEnabled;
        UpdateToggle.IsChecked = settings.Behavior.CheckUpdatesOnStartup;
        currentShortcut = settings.Input.CustomShortcut;
        CaptureShortcutButton.Content = $"点击后自定义快捷唤出　　{(currentShortcut ?? "未设置")}";
        ExclusionTextBox.Text = string.Join(Environment.NewLine, settings.Input.ExcludedApplications);
        DataPathText.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HuahaiClipboard");
        applyingSettings = false;
    }

    private Task PersistAppearanceAsync() => ViewModel is null
        ? Task.CompletedTask
        : ViewModel.UpdateAppearanceAsync(
            ViewModel.ThemeId,
            OpacitySlider.Value / 100d,
            ScaleSlider.Value / 100d);

    private Task PersistMotionAsync() => ViewModel is null
        ? Task.CompletedTask
        : ViewModel.UpdateMotionAsync(
            PetalToggle.IsChecked == true ? PetalLevel.Low : PetalLevel.Off,
            ReducedMotionToggle.IsChecked == true,
            (int)Math.Round(DurationSlider.Value));

    private Task PersistInputAsync(bool? hotkeyEnabled = null)
    {
        if (ViewModel is null) return Task.CompletedTask;
        var exclusions = ExclusionTextBox.Text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ViewModel.UpdateInputAsync(
            RightDoubleToggle.IsChecked == true,
            hotkeyEnabled ?? !string.IsNullOrWhiteSpace(currentShortcut),
            exclusions,
            currentShortcut);
    }

    private Task PersistBehaviorAsync() => ViewModel is null
        ? Task.CompletedTask
        : ViewModel.UpdateBehaviorAsync(
            BackgroundToggle.IsChecked == true,
            ViewModel.RetentionDays,
            UpdateToggle.IsChecked == true);

    private static string? FormatKeyboardGesture(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        var isFunctionKey = key is >= Key.F1 and <= Key.F24;
        if (parts.Count == 0 && !isFunctionKey) return null;
        parts.Add(key.ToString());
        var gesture = string.Join(" + ", parts);
        return ShortcutGestureParser.TryParse(gesture, out _) ? gesture : null;
    }

    private static void RestartTimer(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastChrome.Visibility = Visibility.Visible;
        toastTimer.Stop();
        toastTimer.Start();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }
}
