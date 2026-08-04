using System.ComponentModel;
using System.Diagnostics;
using HuahaiClipboard.App.Infrastructure.Input;
using HuahaiClipboard.App.Infrastructure.Tray;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Services;
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
    private const int PanelCornerRadius = 29;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmBorderColor = 34;
    private const int DwmRoundCornerPreference = 2;
    private const int DwmColorNone = unchecked((int)0xFFFFFFFE);
    private const int WindowStyleIndex = -16;
    private const int ExtendedWindowStyleIndex = -20;
    private const uint SetWindowFrameChanged = 0x0020;
    private const uint SetWindowNoSize = 0x0001;
    private const uint SetWindowNoMove = 0x0002;
    private const uint SetWindowNoZOrder = 0x0004;
    private const uint SetWindowNoActivate = 0x0010;

    private readonly CompositionRoot compositionRoot = new();
    private readonly WindowNavigator navigator = new();
    private readonly List<(Ellipse Petal, double Speed)> petals = [];
    private readonly PanelViewModel panelViewModel;
    private readonly SettingsViewModel settingsViewModel;
    private readonly ClickFeedbackController clickFeedbackController = new();
    private GlobalInputService? globalInputService;
    private TrayService? trayService;
    private InputSettingsSnapshot? inputSettingsSnapshot;
    private CancellationTokenSource? pendingRecordClick;
    private AppWindow? appWindow;
    private bool allowClose;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? petalTimer;
    private bool isPanelVisible = true;
    private bool applyingSettings = true;
    private string? runtimeWarning;
    private ThemeDefinition currentTheme = ThemeCatalog.All[0];
    private double currentOpacity = ShellSettings.Default.Appearance.Opacity;

    public CursorPanelWindow()
    {
        panelViewModel = compositionRoot.CreatePanel(navigator);
        settingsViewModel = compositionRoot.CreateSettings();
        InitializeComponent();
        applyingSettings = false;
        navigator.SettingsAction = ShowSettingsPane;
        navigator.HideTransientPanelAction = HideTransientPanel;
        panelViewModel.PropertyChanged += PanelViewModel_PropertyChanged;
        Closed += (_, _) => DisposeRuntime();
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

    public async Task InitializeRuntimeAsync()
    {
        if (globalInputService is not null)
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var settings = await compositionRoot.SettingsStore.LoadAsync(CancellationToken.None);
        await UnmanagedCallbackGuard.InvokeAsync(
            () => compositionRoot.ImageStore.ProtectLegacyFilesAsync(CancellationToken.None),
            _ => runtimeWarning = "部分旧图片缓存无法加密，请检查本机存储目录权限。");
        inputSettingsSnapshot = new InputSettingsSnapshot(settings.Input);
        compositionRoot.CaptureService.HistoryChanged += CaptureService_HistoryChanged;
        globalInputService = new GlobalInputService(
            handle,
            DispatcherQueue,
            inputSettingsSnapshot,
            compositionRoot.CaptureService,
            ShowAtCursor);
        runtimeWarning = string.Join(
            " ",
            new[] { runtimeWarning }
                .Concat(globalInputService.InitializationWarnings)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        UpdateRecoveryInfoBar();
        trayService = new TrayService(
            () => DispatcherQueue.TryEnqueue(ShowAtCurrentCursor),
            () => DispatcherQueue.TryEnqueue(() =>
            {
                ShowAtCurrentCursor();
                ShowSettingsPane();
            }),
            () => DispatcherQueue.TryEnqueue(ExitApplication));
    }

    public void ShowFromShortcut() => ShowAtCurrentCursor();

    private async Task InitializeAsync()
    {
        ConfigureWindow();
        await settingsViewModel.LoadAsync();
        currentOpacity = GlassOpacityPolicy.Normalize(settingsViewModel.Draft.Appearance.Opacity);
        ApplyTheme(ThemeCatalog.All.FirstOrDefault(theme =>
            theme.Id == settingsViewModel.Draft.Appearance.ThemeId) ?? currentTheme);
        CreatePetals();
        UpdateAmbientMotion();
        await panelViewModel.LoadAsync();
        RecordList.ItemsSource = panelViewModel.VisibleRecords;
        HistorySummary.Text = $"最近 7 天 · {panelViewModel.AllRecords.Count} 条";
        DataPathTextBox.Text = compositionRoot.DataLayout.DataDirectory;
    }

    public async Task RefreshHistoryAsync()
    {
        await panelViewModel.LoadAsync();
        HistorySummary.Text = $"最近 7 天 · {panelViewModel.AllRecords.Count} 条";
    }

    private void HideTransientPanel()
    {
        isPanelVisible = false;
        UpdateAmbientMotion();
        GetAppWindow()?.Hide();
    }

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
        appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new SizeInt32(430, 680));
        if (appWindow is not null)
        {
            appWindow.Closing += AppWindow_Closing;
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            ApplyNativeGlassChrome(handle, 430, 680);

            var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var workArea = display.WorkArea;
            appWindow.Move(new PointInt32(workArea.X + workArea.Width - 446, workArea.Y + 24));
        }
    }

    private void PanelViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanelViewModel.VisibleRecords) or nameof(PanelViewModel.RecoveryMessage))
        {
            RecordList.ItemsSource = panelViewModel.VisibleRecords;
            UpdateRecoveryInfoBar();
        }
    }

    private void UpdateRecoveryInfoBar()
    {
        var messages = new[] { runtimeWarning, panelViewModel.RecoveryMessage }
            .Where(message => !string.IsNullOrWhiteSpace(message));
        RecoveryInfoBar.Message = string.Join(" ", messages);
        RecoveryInfoBar.IsOpen = !string.IsNullOrWhiteSpace(RecoveryInfoBar.Message);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        panelViewModel.SearchText = SearchBox.Text;

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string tag } selected &&
            Enum.TryParse<ClipboardFilter>(tag, out var filter))
        {
            panelViewModel.SelectedFilter = filter;
            foreach (var toggle in FilterBar.Children.OfType<ToggleButton>())
            {
                toggle.IsChecked = ReferenceEquals(toggle, selected);
            }
        }
    }

    private async void RecordList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ClipboardRecord record)
        {
            panelViewModel.SelectRecord(record);
            pendingRecordClick?.Cancel();
            var click = new CancellationTokenSource();
            pendingRecordClick = click;
            var container = RecordList.ContainerFromItem(record) as DependencyObject;
            var card = FindDescendant<Border>(container, "RecordCard");
            try
            {
                await clickFeedbackController.RunAsync(
                    settingsViewModel.Draft.Motion.ReduceMotion,
                    (duration, token) => PlayClickFeedbackAsync(card, duration, token),
                    token => panelViewModel.CopyAsync(record, token),
                    click.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowRuntimeError($"复制失败：{exception.Message}");
            }
        }
    }

    private async void RecordRow_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        pendingRecordClick?.Cancel();
        var click = new CancellationTokenSource();
        pendingRecordClick = click;
        if (sender is Border { DataContext: ClipboardRecord record } card)
        {
            try
            {
                await clickFeedbackController.RunAsync(
                    settingsViewModel.Draft.Motion.ReduceMotion,
                    (duration, token) => PlayClickFeedbackAsync(card, duration, token),
                    token => panelViewModel.PasteAsync(record, token),
                    click.Token);
                e.Handled = true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ShowRuntimeError($"粘贴失败：{exception.Message}");
            }
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
            await RunRowMutationAsync(() => panelViewModel.ToggleFavoriteAsync(record));
        }
    }

    private async void Pin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardRecord record })
        {
            await RunRowMutationAsync(() => panelViewModel.TogglePinnedAsync(record));
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ClipboardRecord record })
        {
            await RunRowMutationAsync(() => panelViewModel.DeleteAsync(record));
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
            RightDoubleClickCheckBox.IsChecked = settingsViewModel.Draft.Input.RightDoubleClickEnabled;
            HotkeyCheckBox.IsChecked = settingsViewModel.Draft.Input.HotkeyEnabled;
            ExcludedAppsTextBox.Text = string.Join(Environment.NewLine, settingsViewModel.Draft.Input.ExcludedApplications);
            DataPathTextBox.Text = compositionRoot.DataLayout.DataDirectory;
            currentOpacity = GlassOpacityPolicy.Normalize(settingsViewModel.Draft.Appearance.Opacity);
            ApplyTheme(ThemeCatalog.All.FirstOrDefault(theme => theme.Id == settingsViewModel.Draft.Appearance.ThemeId) ?? currentTheme);
            SaveStatusText.Text = settingsViewModel.SaveStatus;
            SettingsNavigation.SelectedIndex = 0;
            ShowSettingsCategory("Appearance");
        }
        finally
        {
            applyingSettings = false;
            ResizeWindow(760, 620);
            MainPane.Visibility = Visibility.Collapsed;
            SettingsPane.Visibility = Visibility.Visible;
        }
    }

    private void BackFromSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPane.Visibility = Visibility.Collapsed;
        MainPane.Visibility = Visibility.Visible;
        ResizeWindow(430, 680);
        SearchBox.Focus(FocusState.Programmatic);
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

        ApplySurfaceOpacity(e.NewValue);
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
        UpdateAmbientMotion();
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
        UpdateAmbientMotion();
        SaveStatusText.Text = settingsViewModel.SaveStatus;
    }

    private async void InputCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (applyingSettings)
        {
            return;
        }

        await SaveInputSettingsAsync();
    }

    private async void ExcludedAppsTextBox_LostFocus(object sender, RoutedEventArgs e) =>
        await SaveInputSettingsAsync();

    private async Task SaveInputSettingsAsync()
    {
        var excluded = ExcludedAppsTextBox.Text
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var inputSettings = new InputSettings(
            RightDoubleClickCheckBox.IsChecked == true,
            HotkeyCheckBox.IsChecked == true,
            excluded);
        await settingsViewModel.UpdateInputAsync(inputSettings);
        if (globalInputService is not null)
        {
            globalInputService.UpdateInputSettings(inputSettings);
        }
        else
        {
            inputSettingsSnapshot?.Update(inputSettings);
        }
        runtimeWarning = globalInputService is null
            ? runtimeWarning
            : string.Join(" ", globalInputService.InitializationWarnings);
        UpdateRecoveryInfoBar();
        SaveStatusText.Text = settingsViewModel.SaveStatus;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideTransientPanel();

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
                HideTransientPanel();
            }

            e.Handled = true;
        }
    }

    private void CaptureService_HistoryChanged(object? sender, EventArgs e) =>
        _ = RefreshHistoryAsync();

    private void SettingsNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppearanceSettingsPage is null)
        {
            return;
        }

        if (SettingsNavigation.SelectedItem is ListViewItem { Tag: string category })
        {
            ShowSettingsCategory(category);
        }
    }

    private void ShowSettingsCategory(string category)
    {
        AppearanceSettingsPage.Visibility = category == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        MotionSettingsPage.Visibility = category == "Motion" ? Visibility.Visible : Visibility.Collapsed;
        InputSettingsPage.Visibility = category == "Input" ? Visibility.Visible : Visibility.Collapsed;
        StorageSettingsPage.Visibility = category == "Storage" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenDataDirectory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(compositionRoot.DataLayout.DataDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = compositionRoot.DataLayout.DataDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ShowRuntimeError($"无法打开数据目录：{exception.Message}");
        }
    }

    private void ShowAtCurrentCursor()
    {
        if (GetCursorPos(out var point))
        {
            ShowAtCursor(IntPtr.Zero, new PointInt32(point.X, point.Y));
        }
    }

    private void ShowAtCursor(IntPtr targetWindow, PointInt32 cursor)
    {
        compositionRoot.ClipboardPlatform.SetPasteTarget(targetWindow);
        var display = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        const int width = 430;
        const int height = 680;
        var x = cursor.X + 14;
        if (x + width > workArea.X + workArea.Width)
        {
            x = cursor.X - width - 14;
        }

        x = Math.Clamp(x, workArea.X, workArea.X + workArea.Width - width);
        var y = Math.Clamp(cursor.Y - 48, workArea.Y, workArea.Y + workArea.Height - height);
        appWindow?.MoveAndResize(new RectInt32(x, y, width, height));
        MainPane.Visibility = Visibility.Visible;
        SettingsPane.Visibility = Visibility.Collapsed;
        appWindow?.Show();
        isPanelVisible = true;
        UpdateAmbientMotion();
        Activate();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (allowClose)
        {
            return;
        }

        args.Cancel = true;
        isPanelVisible = false;
        UpdateAmbientMotion();
        sender.Hide();
    }

    private void ExitApplication()
    {
        allowClose = true;
        DisposeRuntime();
        Close();
        Application.Current.Exit();
    }

    private void DisposeRuntime()
    {
        pendingRecordClick?.Cancel();
        StopPetalAnimations();
        compositionRoot.CaptureService.HistoryChanged -= CaptureService_HistoryChanged;
        globalInputService?.Dispose();
        globalInputService = null;
        trayService?.Dispose();
        trayService = null;
    }

    private void ApplyTheme(ThemeDefinition theme)
    {
        currentTheme = theme;
        var accent = ParseColor(theme.Accent);
        var reflection = ParseColor(theme.Reflection);
        var top = ParseColor(theme.GlassTop);
        var bottom = ParseColor(theme.GlassBottom);
        var focus = ParseColor(theme.FocusBorder);
        var contentLens = ParseColor(theme.ContentLens);
        GlassBorder.BorderBrush = new SolidColorBrush(focus);
        GlassBorder.Background = new LinearGradientBrush
        {
            StartPoint = new global::Windows.Foundation.Point(0, 0),
            EndPoint = new global::Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = top, Offset = 0 },
                new GradientStop { Color = bottom, Offset = 1 }
            }
        };
        MainPane.Background = new SolidColorBrush(contentLens);
        ApplySurfaceOpacity(currentOpacity);
        PrimaryReflectionStop.Color = WithAlpha(reflection, 194);
        SecondaryReflectionStop.Color = WithAlpha(reflection, 167);
        SurfaceHighlightStop.Color = WithAlpha(reflection, 181);
        UpdateResourceBrush("HuahaiAccentBrush", accent);
        UpdateResourceBrush("HuahaiFocusBrush", focus);
    }

    private void ApplySurfaceOpacity(double opacity)
    {
        currentOpacity = GlassOpacityPolicy.Normalize(opacity);
        if (GlassBorder.Background is Brush glassBrush)
        {
            glassBrush.Opacity = currentOpacity;
        }

        if (MainPane.Background is Brush contentBrush)
        {
            contentBrush.Opacity = currentOpacity;
        }
    }

    private void UpdateAmbientMotion()
    {
        var activity = PanelActivityPolicy.Resolve(
            isPanelVisible,
            settingsViewModel.Draft.Motion.ReduceMotion,
            settingsViewModel.Draft.Motion.PetalLevel);

        if (!activity.AnimateLiquidReflection)
        {
            LiquidFlowStoryboard.Stop();
            PrimaryReflectionTransform.TranslateX = 0;
            SecondaryReflectionTransform.TranslateX = 0;
            SurfaceHighlightTransform.TranslateX = 0;
        }
        else
        {
            LiquidFlowStoryboard.Begin();
        }

        if (activity.AnimatePetals)
        {
            StartPetalAnimations();
        }
        else
        {
            StopPetalTimer();
        }
    }

    private void CreatePetals()
    {
        StopPetalAnimations();
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

            petals.Add((petal, 0.35 + random.NextDouble() * 0.45));
        }
    }

    private void StopPetalAnimations()
    {
        StopPetalTimer();
        petals.Clear();
    }

    private void StartPetalAnimations()
    {
        if (petalTimer is not null || petals.Count == 0)
        {
            return;
        }

        petalTimer = DispatcherQueue.CreateTimer();
        petalTimer.Interval = TimeSpan.FromMilliseconds(50);
        petalTimer.IsRepeating = true;
        petalTimer.Tick += PetalTimer_Tick;
        petalTimer.Start();
    }

    private void StopPetalTimer()
    {
        if (petalTimer is not null)
        {
            petalTimer.Stop();
            petalTimer.Tick -= PetalTimer_Tick;
            petalTimer = null;
        }
    }

    private void PetalTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        foreach (var (petal, speed) in petals)
        {
            var top = Canvas.GetTop(petal) + speed;
            if (top > PetalCanvas.ActualHeight + 12)
            {
                top = -12;
            }

            Canvas.SetTop(petal, top);
        }
    }

    private async Task RunRowMutationAsync(Func<Task> mutation)
    {
        try
        {
            await mutation();
            HistorySummary.Text = $"最近 7 天 · {panelViewModel.AllRecords.Count} 条";
        }
        catch (Exception exception)
        {
            ShowRuntimeError($"操作未保存：{exception.Message}");
        }
    }

    private void ShowRuntimeError(string message)
    {
        runtimeWarning = message;
        UpdateRecoveryInfoBar();
    }

    private async Task PlayClickFeedbackAsync(
        Border? card,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (card is null)
        {
            await Task.Delay(duration, cancellationToken);
            return;
        }

        var cardTransform = card.RenderTransform as CompositeTransform ?? new CompositeTransform();
        card.RenderTransform = cardTransform;
        var icon = FindDescendant<Border>(card, "RecordTypeIcon");
        var iconTransform = icon?.RenderTransform as CompositeTransform;
        var ripple = FindDescendant<Ellipse>(card, "ClickRipple");
        var rippleTransform = ripple?.RenderTransform as ScaleTransform;
        var half = TimeSpan.FromTicks(duration.Ticks / 2);
        var reduced = duration <= TimeSpan.FromMilliseconds(120);
        var storyboard = new Storyboard();

        AddAutoReverseAnimation(storyboard, cardTransform, "TranslateY", 0, reduced ? -2 : -6, half);
        AddAutoReverseAnimation(storyboard, cardTransform, "ScaleX", 1, reduced ? 1.006 : 1.012, half);
        AddAutoReverseAnimation(storyboard, cardTransform, "ScaleY", 1, reduced ? 1.006 : 1.012, half);

        if (!reduced && iconTransform is not null)
        {
            AddAutoReverseAnimation(storyboard, iconTransform, "ScaleX", 1, 1.23, half);
            AddAutoReverseAnimation(storyboard, iconTransform, "ScaleY", 1, 1.23, half);
            AddAutoReverseAnimation(storyboard, iconTransform, "Rotation", 0, -5, half);
        }

        if (!reduced && ripple is not null && rippleTransform is not null)
        {
            ripple.Opacity = 0.82;
            var rippleDuration = duration - TimeSpan.FromMilliseconds(55);
            AddAnimation(storyboard, rippleTransform, "ScaleX", 0, 24, rippleDuration, TimeSpan.FromMilliseconds(55));
            AddAnimation(storyboard, rippleTransform, "ScaleY", 0, 24, rippleDuration, TimeSpan.FromMilliseconds(55));
            AddAnimation(storyboard, ripple, "Opacity", 0.82, 0, rippleDuration, TimeSpan.FromMilliseconds(55));
        }

        try
        {
            storyboard.Begin();
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            storyboard.Stop();
            cardTransform.TranslateY = 0;
            cardTransform.ScaleX = 1;
            cardTransform.ScaleY = 1;
            if (iconTransform is not null)
            {
                iconTransform.ScaleX = 1;
                iconTransform.ScaleY = 1;
                iconTransform.Rotation = 0;
            }

            if (ripple is not null && rippleTransform is not null)
            {
                ripple.Opacity = 0;
                rippleTransform.ScaleX = 0;
                rippleTransform.ScaleY = 0;
            }
        }
    }

    private static void AddAutoReverseAnimation(
        Storyboard storyboard,
        DependencyObject target,
        string property,
        double from,
        double to,
        TimeSpan halfDuration)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = halfDuration,
            AutoReverse = true,
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private static void AddAnimation(
        Storyboard storyboard,
        DependencyObject target,
        string property,
        double from,
        double to,
        TimeSpan duration,
        TimeSpan beginTime)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            BeginTime = beginTime,
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private static T? FindDescendant<T>(DependencyObject? root, string name)
        where T : FrameworkElement
    {
        if (root is null)
        {
            return null;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            var nested = FindDescendant<T>(child, name);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void ResizeWindow(int width, int height)
    {
        if (appWindow is null)
        {
            return;
        }

        var windowId = appWindow.Id;
        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        var current = appWindow.Position;
        var x = Math.Clamp(current.X, workArea.X, workArea.X + workArea.Width - width);
        var y = Math.Clamp(current.Y, workArea.Y, workArea.Y + workArea.Height - height);
        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        ApplyNativeGlassChrome(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            width,
            height);
    }

    private static void ApplyNativeGlassChrome(IntPtr windowHandle, int width, int height)
    {
        RemoveNativeWindowFrame(windowHandle);

        var cornerPreference = DwmRoundCornerPreference;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));

        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmBorderColor,
            ref borderColor,
            sizeof(int));

        var geometry = WindowChromeGeometry.Create(
            width,
            height,
            PanelCornerRadius,
            GetDpiForWindow(windowHandle));
        var region = CreateRoundRectRgn(
            0,
            0,
            geometry.Width + 1,
            geometry.Height + 1,
            geometry.CornerDiameter,
            geometry.CornerDiameter);
        if (region == IntPtr.Zero)
        {
            return;
        }

        if (SetWindowRgn(windowHandle, region, true) == 0)
        {
            _ = DeleteObject(region);
        }
    }

    private static void RemoveNativeWindowFrame(IntPtr windowHandle)
    {
        var styles = WindowChromeStyle.WithoutNativeFrame(
            GetWindowLongPtr(windowHandle, WindowStyleIndex).ToInt64(),
            GetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex).ToInt64());
        _ = SetWindowLongPtr(windowHandle, WindowStyleIndex, new IntPtr(styles.Style));
        _ = SetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex, new IntPtr(styles.ExtendedStyle));
        _ = SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SetWindowFrameChanged |
            SetWindowNoSize |
            SetWindowNoMove |
            SetWindowNoZOrder |
            SetWindowNoActivate);
    }

    private void DragRegion_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not UIElement element ||
            !e.GetCurrentPoint(element).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = ReleaseCapture();
        _ = SendMessage(handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        e.Handled = true;
    }

    private static void UpdateResourceBrush(string key, global::Windows.UI.Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
        }
        else
        {
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }

    private static global::Windows.UI.Color WithAlpha(global::Windows.UI.Color color, byte alpha) =>
        ColorHelper.FromArgb(alpha, color.R, color.G, color.B);

    private static global::Windows.UI.Color ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        var alpha = byte.Parse(value[..2], global::System.Globalization.NumberStyles.HexNumber);
        var red = byte.Parse(value[2..4], global::System.Globalization.NumberStyles.HexNumber);
        var green = byte.Parse(value[4..6], global::System.Globalization.NumberStyles.HexNumber);
        var blue = byte.Parse(value[6..8], global::System.Globalization.NumberStyles.HexNumber);
        return ColorHelper.FromArgb(alpha, red, green, blue);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr windowHandle, IntPtr regionHandle, bool redraw);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int widthEllipse,
        int heightEllipse);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int value,
        int valueSize);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
