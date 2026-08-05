using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HuahaiClipboard.NativeUiSpike.Presentation.Views;

public partial class SettingsView : UserControl
{
    private readonly DispatcherTimer clearAllTimer;
    private readonly DispatcherTimer toastTimer;
    private bool clearAllPending;
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
        Loaded += (_, _) => SelectPage("Appearance", AppearanceNav);
    }

    public event EventHandler? BackRequested;

    public event Action<double>? OpacityRequested;

    public event Action<double>? PanelScaleRequested;

    public event Action<bool>? PetalsChanged;

    public event Action<bool>? ReducedMotionChanged;

    public event Action<string>? ThemeRequested;

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

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string themeId || ViewModel?.SetTheme(themeId) != true) return;
        ThemeRequested?.Invoke(themeId);
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
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleValue is null) return;
        var percent = (int)Math.Round(e.NewValue / 5d) * 5;
        ScaleValue.Text = $"{percent}%";
        PanelScaleRequested?.Invoke(percent / 100d);
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
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle) return;
        var enabled = toggle.IsChecked == true;
        var name = toggle.Tag?.ToString() ?? "选项";
        ShowToast($"{name}：{(enabled ? "已开启" : "已关闭")}");

        if (ReferenceEquals(toggle, PetalToggle)) PetalsChanged?.Invoke(enabled);
        if (name == "减少动态效果") ReducedMotionChanged?.Invoke(enabled);
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
        var modifiers = Keyboard.Modifiers == ModifierKeys.None ? string.Empty : $"{Keyboard.Modifiers} + ";
        CompleteShortcutCapture($"{modifiers}{key}");
        e.Handled = true;
    }

    private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!shortcutCaptureActive) return;
        CompleteShortcutCapture($"鼠标 {e.ChangedButton}");
        e.Handled = true;
    }

    private void CompleteShortcutCapture(string gesture)
    {
        shortcutCaptureActive = false;
        CaptureShortcutButton.Content = $"点击后自定义快捷唤出　　{gesture}";
        ShowToast($"已保存：{gesture}");
    }

    private void ResetShortcut_Click(object sender, RoutedEventArgs e)
    {
        shortcutCaptureActive = false;
        CaptureShortcutButton.Content = "点击后自定义快捷唤出　　未设置";
        ShowToast("已恢复默认右键双击唤出");
    }

    private void SaveExclusions_Click(object sender, RoutedEventArgs e)
    {
        var count = ExclusionTextBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        ShowToast($"应用排除列表已保存（{count} 项模拟数据）");
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        ShowToast("样机不会打开或修改真实数据目录");

    private void Retention_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var days) || ViewModel?.SetRetentionDays(days) != true) return;
        ShowToast($"自动清理期限已设为 {(days == 30 ? "1 个月" : $"{days} 天")}");
    }

    private void ClearOrdinary_Click(object sender, RoutedEventArgs e)
    {
        var removed = ViewModel?.ClearOrdinary() ?? 0;
        ShowToast($"已清空 {removed} 条普通记录，收藏和置顶已保留");
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (!clearAllPending)
        {
            clearAllPending = true;
            ClearAllButton.Content = "再次点击确认";
            clearAllTimer.Stop();
            clearAllTimer.Start();
            ShowToast("再次点击将删除全部样机记录");
            return;
        }

        var removed = ViewModel?.ClearAll() ?? 0;
        ResetClearAllConfirmation();
        ShowToast($"已清空全部 {removed} 条样机记录");
    }

    private void ResetClearAllConfirmation()
    {
        clearAllTimer.Stop();
        clearAllPending = false;
        ClearAllButton.Content = "清空全部";
    }

    private void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatus.Text = "检查完成：隔离样机不会联网。正式版将读取 GitHub Release 元数据并只提醒用户。";
        ShowToast("更新检查体验已完成（未联网）");
    }

    private void Release_Click(object sender, RoutedEventArgs e) =>
        ShowToast("样机不会打开外部链接；正式版将由用户确认后打开 Release 页面");

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
