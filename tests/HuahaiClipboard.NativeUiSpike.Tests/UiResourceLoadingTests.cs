using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using HuahaiClipboard.NativeUiSpike.Presentation.Views;
using HuahaiClipboard.NativeUiSpike.Presentation.Windows;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class UiResourceLoadingTests
{
    private static Dispatcher? dispatcher;
    private static Thread? wpfThread;

    [ClassInitialize]
    public static void StartWpfDispatcher(TestContext context)
    {
        using var ready = new ManualResetEventSlim();
        wpfThread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "HuahaiClipboard.NativeUiSpike.Tests.WpfDispatcher",
        };
        wpfThread.SetApartmentState(ApartmentState.STA);
        wpfThread.Start();
        Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(5)), "WPF test dispatcher did not start");
    }

    [ClassCleanup]
    public static void StopWpfDispatcher()
    {
        dispatcher?.InvokeShutdown();
        wpfThread?.Join(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void GeneratedDictionary_LoadsLockedPanelGeometry()
    {
        InvokeOnWpfThread(() =>
        {
            var dictionary = new ResourceDictionary
            {
                Source = new Uri(
                    "/HuahaiClipboard.NativeUiSpike;component/Generated/HuahaiUiTokens.xaml",
                    UriKind.Relative),
            };

            Assert.AreEqual(430d, dictionary["HuahaiPanelWidth"]);
            Assert.AreEqual(680d, dictionary["HuahaiPanelHeight"]);
            Assert.AreEqual(new CornerRadius(29), dictionary["HuahaiPanelCornerRadius"]);
            Assert.AreEqual(5, dictionary["HuahaiThemeCount"]);
        });
    }

    [TestMethod]
    public void GlassOpacity_ChangesMaterialWithoutFadingPanelContent()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var window = new MainWindow();

            window.SetGlassMaterialOpacity(0.73);

            Assert.AreEqual(0.73, window.GlassMaterialOpacity, 0.001);
            Assert.AreEqual(1, window.PanelContentOpacity, 0.001);
            window.Close();
        });
    }

    [TestMethod]
    public void DefaultGlassSurface_IsTranslucentAndHasNoOuterBorder()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var window = new MainWindow();

            Assert.IsTrue(window.GlassMaterialOpacity <= 0.82, "The desktop must remain visible through the material");
            Assert.AreEqual(new Thickness(0), window.GlassEdgeThickness, "The liquid-glass shell must not draw a white frame");
            window.Close();
        });
    }

    [TestMethod]
    public void PanelContent_UsesApprovedDarkTranslucentLens()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var window = new MainWindow();
            window.Show();
            window.UpdateLayout();

            var panel = FindVisualChildren<PanelView>(window).Single();
            var lens = panel.Background as SolidColorBrush;

            Assert.IsNotNull(lens, "The panel needs the same translucent content lens as product-shell.html");
            Assert.AreEqual(Color.FromArgb(0x6B, 0x23, 0x0E, 0x29), lens.Color);
            window.Close();
        });
    }

    [TestMethod]
    public void AmbientGlass_UsesWideDiagonalRefractionWithoutWhiteCornerSpotlight()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var window = new MainWindow();
            window.Show();
            window.UpdateLayout();

            var reflection = window.FindName("LiquidReflection") as Border;
            Assert.IsNotNull(reflection);
            Assert.IsTrue(reflection.Width >= 650, "The reflection must span the panel like the approved CSS layer");
            Assert.IsTrue(reflection.Height is >= 180 and <= 240);
            Assert.IsInstanceOfType<BlurEffect>(reflection.Effect);
            Assert.IsTrue(((BlurEffect)reflection.Effect).Radius >= 14);
            Assert.IsInstanceOfType<TransformGroup>(reflection.RenderTransform);
            var rotation = ((TransformGroup)reflection.RenderTransform).Children.OfType<RotateTransform>().Single();
            Assert.AreEqual(22, rotation.Angle, 0.001);

            var hasWhiteCornerSpotlight = FindVisualChildren<Border>(window).Any(border =>
                border.HorizontalAlignment == HorizontalAlignment.Right &&
                border.VerticalAlignment == VerticalAlignment.Bottom &&
                border.Background is RadialGradientBrush radial &&
                radial.GradientStops.Any(stop => stop.Color.R == 255 && stop.Color.G == 255 && stop.Color.B == 255 && stop.Color.A > 0));
            Assert.IsFalse(hasWhiteCornerSpotlight, "The approved liquid glass has no large white corner reflection patch");
            window.Close();
        });
    }

    [TestMethod]
    public void SettingsSurface_UsesApprovedEightTwentyBySixFiftyGeometry()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var window = new MainWindow();

            window.ShowSettings();

            Assert.AreEqual(820, window.Width, 0.001);
            Assert.AreEqual(650, window.Height, 0.001);
            window.Close();
        });
    }

    [TestMethod]
    public void ClipboardList_UsesCompactThemeScrollbarInsteadOfSystemChrome()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var window = new MainWindow();
            window.Show();
            window.UpdateLayout();

            var verticalBar = FindVisualChildren<ScrollBar>(window)
                .FirstOrDefault(bar => bar.Orientation == Orientation.Vertical && bar.IsVisible);

            Assert.IsNotNull(verticalBar, "A vertical clipboard-list scrollbar should be visible for 1,000 records");
            Assert.IsTrue(verticalBar.ActualWidth <= 8, "The scrollbar must not use wide system chrome");
            window.Close();
        });
    }

    [TestMethod]
    public void ReducedMotionAndPetalControls_StopAmbientLayers()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var window = new MainWindow();

            window.SetPetalsEnabled(false);
            window.SetReducedMotion(true);

            Assert.IsFalse(window.PetalsVisible);
            Assert.IsFalse(window.AmbientMotionEnabled);
            window.Close();
        });
    }

    [TestMethod]
    public void ClearAllFeedback_UsesProductionHistoryWording()
    {
        InvokeOnWpfThread(() =>
        {
            EnsureApplicationResources();
            var settings = new SettingsView();
            var host = new Window { Content = settings };
            host.Show();
            host.UpdateLayout();

            var clearAll = FindVisualChildren<Button>(settings)
                .Single(button => Equals(button.Content, "清空全部"));
            clearAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            clearAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var visibleMessages = FindVisualChildren<TextBlock>(settings)
                .Where(text => text.Visibility == Visibility.Visible)
                .Select(text => text.Text)
                .ToArray();
            Assert.IsFalse(
                visibleMessages.Any(message => message.Contains("样机", StringComparison.Ordinal)),
                "Production feedback must describe clipboard history, not prototype fixture records.");
            host.Close();
        });
    }

    private static void EnsureApplicationResources()
    {
        if (Application.Current.Resources.Contains("HuahaiTextBoxStyle")) return;

        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/HuahaiClipboard.NativeUiSpike;component/Generated/HuahaiUiTokens.xaml",
                UriKind.Relative),
        });
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/HuahaiClipboard.NativeUiSpike;component/Presentation/Styles/NativeUiStyles.xaml",
                UriKind.Relative),
        });
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "/HuahaiClipboard.NativeUiSpike;component/Presentation/Controls/GlassSurface.xaml",
                UriKind.Relative),
        });
    }

    private static void InvokeOnWpfThread(Action action)
    {
        Assert.IsNotNull(dispatcher, "WPF test dispatcher is not initialized");
        dispatcher.Invoke(action);
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
