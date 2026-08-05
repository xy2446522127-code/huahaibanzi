using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HuahaiClipboard.NativeUiSpike.Presentation.Controls;
using HuahaiClipboard.NativeUiSpike.Presentation.Views;
using HuahaiClipboard.NativeUiSpike.Services;
using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.NativeUiSpike.Presentation.Windows;

public partial class MainWindow : Window, IPanelWindowHost
{
    private const double PanelHeight = 680;
    private const double PanelWidth = 430;
    private const double SettingsHeight = 650;
    private const double SettingsWidth = 820;

    private readonly PanelView panel;
    private readonly SettingsView settings;
    private readonly WindowCompositionService composition;
    private readonly NativeUiSpikeViewModel viewModel;
    private bool hasPendingPointerUpdate;
    private bool petalsEnabled = true;
    private bool reducedMotion;
    private bool renderingSubscribed;
    private double currentScale = 1;
    private Point latestPointer;

    public MainWindow() : this(NativeUiSpikeViewModel.CreateFixture(1000))
    {
    }

    public MainWindow(NativeUiSpikeViewModel viewModel)
    {
        InitializeComponent();
        composition = new WindowCompositionService(this);
        StateController = new PanelWindowStateController(this);
        SourceInitialized += (_, _) => composition.Apply();
        SizeChanged += (_, _) => composition.ApplyRoundedRegion();
        PreviewMouseMove += MainWindow_PreviewMouseMove;
        IsVisibleChanged += MainWindow_IsVisibleChanged;
        Closed += (_, _) => StopRenderingUpdates();

        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;

        panel = new PanelView { DataContext = viewModel };
        panel.HideRequested += (_, _) => StateController.Hide();
        panel.SettingsRequested += (_, _) => StateController.OpenSettings();
        PanelHost.Content = panel;

        settings = new SettingsView { DataContext = viewModel };
        settings.BackRequested += (_, _) => StateController.CloseSettings();
        settings.ThemeRequested += ApplyTheme;
        settings.OpacityRequested += SetGlassMaterialOpacity;
        settings.PanelScaleRequested += ApplyScale;
        settings.PetalsChanged += SetPetalsEnabled;
        settings.ReducedMotionChanged += SetReducedMotion;
        settings.StartupChanged += enabled => StartupChanged?.Invoke(enabled);
        SettingsHost.Content = settings;

        ApplyPersistedSettings(viewModel.CurrentSettings);
        viewModel.SettingsChanged += (_, updated) => ApplyPersistedSettings(updated);
    }

    public PanelWindowStateController StateController { get; }

    public event Action<bool>? StartupChanged;

    public NativeUiSpikeViewModel ViewModel => viewModel;

    public void SetStartupState(bool enabled) => settings.SetStartupState(enabled);

    public double GlassMaterialOpacity => GlassRoot.Background?.Opacity ?? 1;

    public double PanelContentOpacity => GlassRoot.Opacity;

    public Thickness GlassEdgeThickness => GlassRoot.BorderThickness;

    public bool PetalsVisible => PetalLayer.Visibility == Visibility.Visible;

    public bool AmbientMotionEnabled => !reducedMotion;

    public void SetPetalsEnabled(bool enabled)
    {
        petalsEnabled = enabled;
        PetalLayer.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        RefreshAmbientMotion();
    }

    public void SetReducedMotion(bool enabled)
    {
        reducedMotion = enabled;
        RefreshAmbientMotion();
    }

    public void SetGlassMaterialOpacity(double opacity)
    {
        if (GlassRoot.Background is not Brush brush) return;
        if (brush.IsFrozen)
        {
            brush = brush.Clone();
            GlassRoot.Background = brush;
        }

        brush.Opacity = Math.Clamp(opacity, 0.65, 0.96);
    }

    public void ShowSettings()
    {
        viewModel.OpenSettings(true);
        PanelHost.Visibility = Visibility.Collapsed;
        SettingsHost.Visibility = Visibility.Visible;
        ApplyWindowSize(SettingsWidth, SettingsHeight);
    }

    public void ShowPanel()
    {
        viewModel.OpenSettings(false);
        SettingsHost.Visibility = Visibility.Collapsed;
        PanelHost.Visibility = Visibility.Visible;
        ApplyWindowSize(PanelWidth, PanelHeight);
    }

    void IPanelWindowHost.CloseSettings() => ShowPanel();

    void IPanelWindowHost.FocusSearch() => panel.FocusSearch();

    void IPanelWindowHost.HideWindow() => Hide();

    void IPanelWindowHost.MoveNear(Point cursor)
    {
        const double offset = 14;
        var desiredLeft = cursor.X + offset;
        var desiredTop = cursor.Y + offset;
        var minimumLeft = SystemParameters.VirtualScreenLeft;
        var minimumTop = SystemParameters.VirtualScreenTop;
        var maximumLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width;
        var maximumTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height;
        Left = Math.Clamp(desiredLeft, minimumLeft, Math.Max(minimumLeft, maximumLeft));
        Top = Math.Clamp(desiredTop, minimumTop, Math.Max(minimumTop, maximumTop));
    }

    void IPanelWindowHost.OpenSettings() => ShowSettings();

    void IPanelWindowHost.RefreshContent()
    {
        _ = viewModel.ReloadHistoryAsync();
    }

    void IPanelWindowHost.SetTopmost(bool enabled) => composition.SetTopmost(enabled);

    void IPanelWindowHost.ShowWindow()
    {
        if (!IsVisible) Show();
        Activate();
        composition.BringToForeground();
    }

    private void ApplyScale(double scale)
    {
        currentScale = Math.Clamp(scale, 0.8, 1.6);
        GlassRoot.LayoutTransform = new ScaleTransform(currentScale, currentScale);
        ApplyWindowSize(viewModel.IsSettingsOpen ? SettingsWidth : PanelWidth, viewModel.IsSettingsOpen ? SettingsHeight : PanelHeight);
    }

    private void MainWindow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        latestPointer = e.GetPosition(this);
        hasPendingPointerUpdate = true;
    }

    private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            StartRenderingUpdates();
            RefreshAmbientMotion();
            return;
        }

        StopAmbientMotion();
        StopRenderingUpdates();
    }

    private void StartRenderingUpdates()
    {
        if (renderingSubscribed) return;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        renderingSubscribed = true;
    }

    private void StopRenderingUpdates()
    {
        if (!renderingSubscribed) return;
        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        renderingSubscribed = false;
        hasPendingPointerUpdate = false;
        SpecularButtonBehavior.Clear(this);
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        if (!hasPendingPointerUpdate || reducedMotion) return;
        hasPendingPointerUpdate = false;
        var proximity = Application.Current.TryFindResource("HuahaiSpecularProximity") is double configured
            ? configured
            : 10d;
        SpecularButtonBehavior.UpdateIntensities(this, latestPointer, proximity);
    }

    private void RefreshAmbientMotion()
    {
        PetalLayer.Visibility = petalsEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (!IsVisible || reducedMotion)
        {
            StopAmbientMotion();
            if (reducedMotion) SpecularButtonBehavior.Clear(this);
            return;
        }

        StartAmbientMotion();
    }

    private void StartAmbientMotion()
    {
        if (LiquidReflection.RenderTransform is TransformGroup group &&
            group.Children.OfType<TranslateTransform>().FirstOrDefault() is { } reflectionTranslation)
        {
            reflectionTranslation.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(0, 130, TimeSpan.FromSeconds(7))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                });
        }

        if (!petalsEnabled) return;
        var petals = new FrameworkElement[] { PetalOne, PetalTwo, PetalThree, PetalFour, PetalFive };
        for (var index = 0; index < petals.Length; index++)
        {
            var petal = petals[index];
            petal.BeginAnimation(
                Canvas.TopProperty,
                new DoubleAnimation(-14 - index * 24, Math.Max(Height, 680) + 16, TimeSpan.FromSeconds(10 + index * 1.4))
                {
                    BeginTime = TimeSpan.FromSeconds(index * 1.25),
                    RepeatBehavior = RepeatBehavior.Forever,
                });
        }
    }

    private void StopAmbientMotion()
    {
        if (LiquidReflection.RenderTransform is TransformGroup group)
        {
            group.Children.OfType<TranslateTransform>().FirstOrDefault()?.BeginAnimation(TranslateTransform.XProperty, null);
        }

        foreach (var petal in new FrameworkElement[] { PetalOne, PetalTwo, PetalThree, PetalFour, PetalFive })
        {
            petal.BeginAnimation(Canvas.TopProperty, null);
        }
    }

    private void ApplyWindowSize(double width, double height)
    {
        Width = width * currentScale;
        Height = height * currentScale;
    }

    private void ApplyPersistedSettings(ShellSettings settings)
    {
        ApplyTheme(settings.Appearance.ThemeId);
        SetGlassMaterialOpacity(settings.Appearance.Opacity);
        ApplyScale(settings.Appearance.PanelScale);
        SetPetalsEnabled(settings.Motion.PetalLevel != PetalLevel.Off);
        SetReducedMotion(settings.Motion.ReduceMotion);
    }

    private static void ApplyTheme(string themeId)
    {
        var prefix = themeId switch
        {
            "cobalt-blue" => "CobaltBlue",
            "emerald-cyan" => "EmeraldCyan",
            "amber-orange" => "AmberOrange",
            "aurora-cyan-purple" => "AuroraCyanPurple",
            _ => "RosePurple",
        };

        var mappings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Accent"] = "HuahaiAccentColor",
            ["Reflection"] = "HuahaiReflectionColor",
            ["GlassTop"] = "HuahaiGlassTopColor",
            ["GlassBottom"] = "HuahaiGlassBottomColor",
            ["ContentLens"] = "HuahaiContentLensColor",
            ["Text"] = "HuahaiTextColor",
            ["Muted"] = "HuahaiMutedTextColor",
        };

        foreach (var (suffix, destination) in mappings)
        {
            if (Application.Current.TryFindResource($"Huahai{prefix}{suffix}Color") is Color color)
            {
                Application.Current.Resources[destination] = color;
            }
        }
    }
}
