using System.ComponentModel;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Settings;
using HuahaiClipboard.Core.Visual;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using TimeSpan = global::System.TimeSpan;
using Random = global::System.Random;

namespace HuahaiClipboard.App.Presentation.Windows;

public sealed partial class CursorPanelWindow : Window
{
    private readonly CompositionRoot compositionRoot = new();
    private readonly WindowNavigator navigator = new();
    private readonly PanelViewModel panelViewModel;
    private readonly SettingsViewModel settingsViewModel;
    private bool applyingSettings;
    private ThemeDefinition currentTheme = ThemeCatalog.All[0];

    public CursorPanelWindow()
    {
        InitializeComponent();
        panelViewModel = compositionRoot.CreatePanel(navigator);
        settingsViewModel = compositionRoot.CreateSettings();
        navigator.SettingsAction = ShowSettingsPane;
        navigator.HideTransientPanelAction = HideTransientPanel;
        panelViewModel.PropertyChanged += PanelViewModel_PropertyChanged;
        _ = InitializeAsync();

        try
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch
        {
            // Static translucent layers remain usable on systems without acrylic support.
        }
    }

    private async Task InitializeAsync()
    {
        ConfigureWindow();
        ApplyTheme(currentTheme);
        CreatePetals();
        await panelViewModel.LoadAsync();
        RecordList.ItemsSource = panelViewModel.VisibleRecords;
    }

    private void HideTransientPanel() => GetAppWindow()?.Hide();

    private AppWindow? GetAppWindow()
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(handle);
        return AppWindow.GetFromWindowId(windowId);
    }

    private void ConfigureWindow()
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(handle);
        AppWindow.GetFromWindowId(windowId)?.Resize(new SizeInt32(430, 680));
    }

    private void PanelViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanelViewModel.VisibleRecords) or nameof(PanelViewModel.RecoveryMessage))
        {
            RecordList.ItemsSource = panelViewModel.VisibleRecords;
            RecoveryInfoBar.Message = panelViewModel.RecoveryMessage ?? string.Empty;
            RecoveryInfoBar.IsOpen = !string.IsNullOrWhiteSpace(panelViewModel.RecoveryMessage);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        panelViewModel.SearchText = SearchBox.Text;

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse<ClipboardFilter>(tag, out var filter))
        {
            panelViewModel.SelectedFilter = filter;
        }
    }

    private async void RecordList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ClipboardRecord record)
        {
            if (e.OriginalSource is FrameworkElement element)
            {
                AnimateClick(element);
            }

            await panelViewModel.CopyAsync(record);
        }
    }

    private async void RecordList_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case global::Windows.System.VirtualKey.Up:
                panelViewModel.MoveSelection(-1);
                e.Handled = true;
                break;
            case global::Windows.System.VirtualKey.Down:
                panelViewModel.MoveSelection(1);
                e.Handled = true;
                break;
            case global::Windows.System.VirtualKey.Enter when panelViewModel.SelectedRecord is not null:
                await panelViewModel.PasteAsync(panelViewModel.SelectedRecord);
                e.Handled = true;
                break;
        }
    }

    private async void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardRecord record })
        {
            await panelViewModel.ToggleFavoriteAsync(record);
        }
    }

    private async void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardRecord record })
        {
            await panelViewModel.TogglePinnedAsync(record);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardRecord record })
        {
            await panelViewModel.DeleteAsync(record);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => ShowSettingsPane();

    private async void ShowSettingsPane()
    {
        applyingSettings = true;
        try
        {
            await settingsViewModel.LoadAsync();
            OpacitySlider.Value = settingsViewModel.Draft.Appearance.Opacity;
            PetalCheckBox.IsChecked = settingsViewModel.Draft.Motion.PetalLevel != PetalLevel.Off;
            ReduceMotionCheckBox.IsChecked = settingsViewModel.Draft.Motion.ReduceMotion;
            ApplyTheme(ThemeCatalog.All.FirstOrDefault(theme => theme.Id == settingsViewModel.Draft.Appearance.ThemeId) ?? currentTheme);
            SaveStatusText.Text = settingsViewModel.SaveStatus;
        }
        finally
        {
            applyingSettings = false;
            MainPane.Visibility = Visibility.Collapsed;
            SettingsPane.Visibility = Visibility.Visible;
        }
    }

    private void BackFromSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPane.Visibility = Visibility.Collapsed;
        MainPane.Visibility = Visibility.Visible;
    }

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string themeId })
        {
            return;
        }

        var theme = ThemeCatalog.All.FirstOrDefault(item => item.Id == themeId);
        if (theme is null)
        {
            return;
        }

        ApplyTheme(theme);
        await settingsViewModel.UpdateAppearanceAsync(
            settingsViewModel.Draft.Appearance with { ThemeId = theme.Id });
        SaveStatusText.Text = settingsViewModel.SaveStatus;
    }

    private async void OpacitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (applyingSettings || settingsViewModel.Draft is null)
        {
            return;
        }

        await settingsViewModel.UpdateAppearanceAsync(
            settingsViewModel.Draft.Appearance with { Opacity = e.NewValue });
        SaveStatusText.Text = settingsViewModel.SaveStatus;
    }

    private async void PetalCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (applyingSettings)
        {
            return;
        }

        var level = PetalCheckBox.IsChecked == true ? PetalLevel.Low : PetalLevel.Off;
        await settingsViewModel.UpdateMotionAsync(settingsViewModel.Draft.Motion with { PetalLevel = level });
        CreatePetals();
        SaveStatusText.Text = settingsViewModel.SaveStatus;
    }

    private async void ReduceMotionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (applyingSettings)
        {
            return;
        }

        await settingsViewModel.UpdateMotionAsync(
            settingsViewModel.Draft.Motion with { ReduceMotion = ReduceMotionCheckBox.IsChecked == true });
        SaveStatusText.Text = settingsViewModel.SaveStatus;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Escape)
        {
            if (SettingsPane.Visibility == Visibility.Visible)
            {
                BackFromSettings_Click(sender, e);
            }
            else
            {
                Close();
            }

            e.Handled = true;
        }
    }

    private void ApplyTheme(ThemeDefinition theme)
    {
        currentTheme = theme;
        var accent = ParseColor(theme.Accent);
        var bottom = ParseColor(theme.GlassBottom);
        var focus = ParseColor(theme.FocusBorder);
        GlassBorder.BorderBrush = new SolidColorBrush(focus);
        GlassBorder.Background = new LinearGradientBrush
        {
            StartPoint = new global::Windows.Foundation.Point(0, 0),
            EndPoint = new global::Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = accent, Offset = 0 },
                new GradientStop { Color = bottom, Offset = 1 }
            }
        };
        Application.Current.Resources["HuahaiAccentBrush"] = new SolidColorBrush(accent);
        Application.Current.Resources["HuahaiFocusBrush"] = new SolidColorBrush(focus);
    }

    private void CreatePetals()
    {
        PetalCanvas.Children.Clear();
        if (settingsViewModel.Draft.Motion.PetalLevel == PetalLevel.Off)
        {
            return;
        }

        var count = settingsViewModel.Draft.Motion.PetalLevel switch
        {
            PetalLevel.Medium => 9,
            PetalLevel.High => 14,
            _ => 5
        };
        var random = new Random(20260804);
        var accent = ParseColor(currentTheme.Accent);
        for (var index = 0; index < count; index++)
        {
            var petal = new Ellipse
            {
                Width = random.Next(5, 8),
                Height = random.Next(3, 6),
                Fill = new SolidColorBrush(accent),
                Opacity = 0.18 + random.NextDouble() * 0.2,
                RenderTransformOrigin = new global::Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new RotateTransform { Angle = random.Next(-35, 36) }
            };
            Canvas.SetLeft(petal, 20 + random.Next(0, 360));
            Canvas.SetTop(petal, random.Next(15, 620));
            PetalCanvas.Children.Add(petal);

            var drift = new TranslateTransform();
            petal.RenderTransform = drift;
            var animation = new DoubleAnimation
            {
                From = -30,
                To = 650,
                Duration = TimeSpan.FromSeconds(14 + random.Next(0, 11)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(animation, drift);
            Storyboard.SetTargetProperty(animation, "Y");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }
    }

    private static void AnimateClick(FrameworkElement element)
    {
        var transform = new CompositeTransform();
        element.RenderTransform = transform;
        var lift = new DoubleAnimationUsingKeyFrames();
        lift.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 0 });
        lift.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)), Value = -3 });
        lift.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(760)), Value = 0 });
        Storyboard.SetTarget(lift, transform);
        Storyboard.SetTargetProperty(lift, "Y");
        var storyboard = new Storyboard();
        storyboard.Children.Add(lift);
        storyboard.Begin();
    }

    private static global::Windows.UI.Color ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        var alpha = byte.Parse(value[..2], global::System.Globalization.NumberStyles.HexNumber);
        var red = byte.Parse(value[2..4], global::System.Globalization.NumberStyles.HexNumber);
        var green = byte.Parse(value[4..6], global::System.Globalization.NumberStyles.HexNumber);
        var blue = byte.Parse(value[6..8], global::System.Globalization.NumberStyles.HexNumber);
        return ColorHelper.FromArgb(alpha, red, green, blue);
    }
}
