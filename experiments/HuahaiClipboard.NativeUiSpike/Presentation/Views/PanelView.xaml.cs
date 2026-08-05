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

    public void FocusSearch() => SearchBox.Focus();

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

    private async void Record_CopyRequested(Guid id)
    {
        var item = ViewModel?.AllItems.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null) return;
        try
        {
            if (await ViewModel!.CopyAsync(id) && AutoHideCheckBox.IsChecked == true)
            {
                HideRequested?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ShowToast("复制失败，请重试");
            }
        }
        catch (Exception exception)
        {
            ShowToast($"复制失败：{exception.Message}");
        }
    }

    private async void Record_PinRequested(Guid id)
    {
        try
        {
            if (ViewModel is null) return;
            await ViewModel.TogglePinnedAsync(id);
            var item = ViewModel.AllItems.FirstOrDefault(candidate => candidate.Id == id);
            ShowToast(item?.IsPinned == true ? "已置顶" : "已取消置顶");
        }
        catch (Exception exception)
        {
            ShowToast($"置顶失败：{exception.Message}");
        }
    }

    private async void Record_FavoriteRequested(Guid id)
    {
        try
        {
            if (ViewModel is null) return;
            await ViewModel.ToggleFavoriteAsync(id);
            var item = ViewModel.AllItems.FirstOrDefault(candidate => candidate.Id == id);
            ShowToast(item?.IsFavorite == true ? "已收藏" : "已取消收藏");
        }
        catch (Exception exception)
        {
            ShowToast($"收藏失败：{exception.Message}");
        }
    }

    private async void Record_DeleteRequested(Guid id)
    {
        try
        {
            if (ViewModel is null) return;
            await ViewModel.DeleteAsync(id);
            ShowToast("记录已删除");
        }
        catch (Exception exception)
        {
            ShowToast($"删除失败：{exception.Message}");
        }
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
