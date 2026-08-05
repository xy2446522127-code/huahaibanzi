using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.NativeUiSpike.Models;

namespace HuahaiClipboard.NativeUiSpike.Presentation.Views;

public partial class PanelView : UserControl
{
    private readonly DispatcherTimer toastTimer;

    public PanelView()
    {
        InitializeComponent();
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        toastTimer.Tick += (_, _) =>
        {
            toastTimer.Stop();
            ToastChrome.Visibility = Visibility.Collapsed;
        };
        Loaded += (_, _) => SetActiveFilter(AllFilter);
    }

    public event EventHandler? HideRequested;

    public event EventHandler? SettingsRequested;

    private NativeUiSpikeViewModel? ViewModel => DataContext as NativeUiSpikeViewModel;

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !Enum.TryParse<ClipboardFilter>(button.Tag?.ToString(), out var filter)) return;

        ViewModel?.SelectFilter(filter);
        SetActiveFilter(button);
    }

    private void SetActiveFilter(Button active)
    {
        foreach (var button in FindVisualChildren<Button>(this).Where(button => button.Tag is not null))
        {
            button.Background = button == active
                ? (Brush)FindResource("HuahaiAccentBrush")
                : new SolidColorBrush(Color.FromArgb(0x52, 0x31, 0x1B, 0x36));
            button.Foreground = button == active ? Brushes.White : (Brush)FindResource("HuahaiMutedTextBrush");
        }
    }

    private void Record_CopyRequested(Guid id)
    {
        var item = ViewModel?.AllItems.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null) return;
        ShowToast($"已模拟复制：{item.Title}");
        if (AutoHideCheckBox.IsChecked == true) HideRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Record_PinRequested(Guid id)
    {
        ViewModel?.TogglePinned(id);
        var item = ViewModel?.AllItems.FirstOrDefault(candidate => candidate.Id == id);
        ShowToast(item?.IsPinned == true ? "已置顶" : "已取消置顶");
    }

    private void Record_FavoriteRequested(Guid id)
    {
        ViewModel?.ToggleFavorite(id);
        var item = ViewModel?.AllItems.FirstOrDefault(candidate => candidate.Id == id);
        ShowToast(item?.IsFavorite == true ? "已收藏" : "已取消收藏");
    }

    private void Record_DeleteRequested(Guid id)
    {
        ViewModel?.Delete(id);
        ShowToast("记录已删除");
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        HideRequested?.Invoke(this, EventArgs.Empty);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

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
