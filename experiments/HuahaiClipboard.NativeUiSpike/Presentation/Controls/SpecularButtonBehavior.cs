using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HuahaiClipboard.NativeUiSpike.Presentation.Controls;

public static class SpecularButtonBehavior
{
    public static readonly DependencyProperty IntensityProperty = DependencyProperty.RegisterAttached(
        "Intensity",
        typeof(double),
        typeof(SpecularButtonBehavior),
        new FrameworkPropertyMetadata(0d));

    public static double GetIntensity(DependencyObject target) =>
        (double)target.GetValue(IntensityProperty);

    public static void SetIntensity(DependencyObject target, double value) =>
        target.SetValue(IntensityProperty, Math.Clamp(value, 0d, 1d));

    public static double CalculateIntensity(Rect bounds, Point pointer, double proximity)
    {
        if (proximity <= 0) return bounds.Contains(pointer) ? 1d : 0d;

        var horizontalDistance = pointer.X < bounds.Left
            ? bounds.Left - pointer.X
            : pointer.X > bounds.Right
                ? pointer.X - bounds.Right
                : 0d;
        var verticalDistance = pointer.Y < bounds.Top
            ? bounds.Top - pointer.Y
            : pointer.Y > bounds.Bottom
                ? pointer.Y - bounds.Bottom
                : 0d;
        var distance = Math.Sqrt(horizontalDistance * horizontalDistance + verticalDistance * verticalDistance);
        return Math.Clamp(1d - distance / proximity, 0d, 1d);
    }

    public static void UpdateIntensities(UIElement root, Point pointer, double proximity)
    {
        foreach (var button in FindVisualChildren<Button>(root))
        {
            if (!button.IsVisible || button.ActualWidth <= 0 || button.ActualHeight <= 0)
            {
                SetIntensity(button, 0);
                continue;
            }

            var origin = button.TranslatePoint(new Point(0, 0), root);
            SetIntensity(button, CalculateIntensity(
                new Rect(origin, new Size(button.ActualWidth, button.ActualHeight)),
                pointer,
                proximity));
        }
    }

    public static void Clear(DependencyObject root)
    {
        foreach (var button in FindVisualChildren<Button>(root)) SetIntensity(button, 0);
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
