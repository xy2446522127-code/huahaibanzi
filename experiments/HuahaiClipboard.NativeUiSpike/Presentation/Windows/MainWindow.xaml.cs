using System.Windows;
using System.Windows.Media;
using HuahaiClipboard.NativeUiSpike.Presentation.Views;

namespace HuahaiClipboard.NativeUiSpike.Presentation.Windows;

public partial class MainWindow : Window
{
    private const double PanelHeight = 680;
    private const double PanelWidth = 430;
    private const double SettingsHeight = 650;
    private const double SettingsWidth = 820;

    private readonly PanelView panel;
    private readonly SettingsView settings;
    private readonly NativeUiSpikeViewModel viewModel;
    private double currentScale = 1;

    public MainWindow()
    {
        InitializeComponent();

        viewModel = NativeUiSpikeViewModel.CreateFixture(1000);
        DataContext = viewModel;

        panel = new PanelView { DataContext = viewModel };
        panel.HideRequested += (_, _) => Hide();
        panel.SettingsRequested += (_, _) => ShowSettings();
        PanelHost.Content = panel;

        settings = new SettingsView { DataContext = viewModel };
        settings.BackRequested += (_, _) => CloseSettings();
        settings.ThemeRequested += ApplyTheme;
        settings.OpacityRequested += SetGlassMaterialOpacity;
        settings.PanelScaleRequested += ApplyScale;
        SettingsHost.Content = settings;

        ApplyTheme("rose-purple");
        ApplyScale(1);
    }

    public double GlassMaterialOpacity => GlassRoot.Background?.Opacity ?? 1;

    public double PanelContentOpacity => GlassRoot.Opacity;

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

    private void CloseSettings()
    {
        viewModel.OpenSettings(false);
        SettingsHost.Visibility = Visibility.Collapsed;
        PanelHost.Visibility = Visibility.Visible;
        ApplyWindowSize(PanelWidth, PanelHeight);
    }

    private void ApplyScale(double scale)
    {
        currentScale = Math.Clamp(scale, 0.8, 1.6);
        GlassRoot.LayoutTransform = new ScaleTransform(currentScale, currentScale);
        ApplyWindowSize(viewModel.IsSettingsOpen ? SettingsWidth : PanelWidth, viewModel.IsSettingsOpen ? SettingsHeight : PanelHeight);
    }

    private void ApplyWindowSize(double width, double height)
    {
        Width = width * currentScale;
        Height = height * currentScale;
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
