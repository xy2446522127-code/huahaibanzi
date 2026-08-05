using System.Windows.Controls;
using System.Windows.Input;
using HuahaiClipboard.NativeUiSpike.Models;

namespace HuahaiClipboard.NativeUiSpike.Presentation.Controls;

public partial class ClipboardRecordView : UserControl
{
    public ClipboardRecordView()
    {
        InitializeComponent();
    }

    public event Action<Guid>? CopyRequested;

    public event Action<Guid>? DeleteRequested;

    public event Action<Guid>? FavoriteRequested;

    public event Action<Guid>? PinRequested;

    private Guid? ItemId => (DataContext as SpikeClipboardItem)?.Id;

    private void RecordChrome_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemId is { } id) CopyRequested?.Invoke(id);
    }

    private void PinButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ItemId is { } id) PinRequested?.Invoke(id);
    }

    private void FavoriteButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ItemId is { } id) FavoriteRequested?.Invoke(id);
    }

    private void DeleteButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ItemId is { } id) DeleteRequested?.Invoke(id);
    }
}
