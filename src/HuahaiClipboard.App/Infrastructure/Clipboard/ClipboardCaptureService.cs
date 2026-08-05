using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Privacy;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using FormsClipboard = System.Windows.Forms.Clipboard;

namespace HuahaiClipboard.App.Infrastructure.Clipboard;

public sealed class ClipboardCaptureService(
    JsonClipboardHistorySource historySource,
    JsonSettingsStore settingsStore,
    IClipboardImageStore imageStore)
{
    private readonly int currentProcessId = Environment.ProcessId;

    public event EventHandler? HistoryChanged;

    public async Task CaptureAsync(IntPtr sourceWindow)
    {
        var identity = WindowIdentity.FromHandle(sourceWindow);
        if (identity.ProcessId == currentProcessId)
        {
            return;
        }

        var settings = await settingsStore.LoadAsync(CancellationToken.None);
        var filter = new ClipboardPrivacyFilter(settings.Input.ExcludedApplications);
        if (filter.ShouldExclude(identity.ProcessName, identity.WindowTitle))
        {
            return;
        }

        ClipboardRecord? record = null;
        for (var attempt = 0; attempt < 5 && record is null; attempt++)
        {
            try
            {
                record = await CreateRecordAsync(identity);
            }
            catch (ExternalException) when (attempt < 4)
            {
                await Task.Delay(30 * (attempt + 1));
            }
        }

        if (record is null ||
            record.Kind is ClipboardItemKind.Text or ClipboardItemKind.Link &&
            filter.ShouldExcludeContent(record.PrimaryText))
        {
            return;
        }

        await historySource.UpsertAsync(record, CancellationToken.None);
        await historySource.PruneAsync(
            DateTimeOffset.Now.AddDays(-settings.Behavior.AutoCleanupDays),
            preserveProtected: true,
            CancellationToken.None);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<ClipboardRecord?> CreateRecordAsync(WindowIdentity identity)
    {
        var copiedAt = DateTimeOffset.Now;
        var source = string.IsNullOrWhiteSpace(identity.ProcessName) ? "未知应用" : identity.ProcessName;
        if (FormsClipboard.ContainsFileDropList())
        {
            StringCollection values = FormsClipboard.GetFileDropList();
            var files = values.Cast<string>().ToArray();
            if (files.Length == 0)
            {
                return null;
            }

            return new ClipboardRecord(
                Guid.NewGuid(),
                ClipboardItemKind.File,
                string.Join(Environment.NewLine, files),
                $"{source} · {files.Length} 个文件",
                copiedAt,
                false,
                false,
                files.All(File.Exists),
                null);
        }

        if (FormsClipboard.ContainsImage())
        {
            using var image = FormsClipboard.GetImage();
            if (image is null)
            {
                return null;
            }

            var imageFileName = ClipboardDisplayName.CreateImageFileName(copiedAt);
            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Png);
            var imagePath = await imageStore.SaveAsync(
                imageFileName,
                stream.ToArray(),
                CancellationToken.None);
            return new ClipboardRecord(
                Guid.NewGuid(),
                ClipboardItemKind.Image,
                Path.GetFileName(imagePath),
                $"{source} · {image.Width} x {image.Height} · PNG",
                copiedAt,
                false,
                false,
                true,
                imagePath);
        }

        if (!FormsClipboard.ContainsText())
        {
            return null;
        }

        var text = FormsClipboard.GetText();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var kind = Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
                   uri.Scheme is "http" or "https"
            ? ClipboardItemKind.Link
            : ClipboardItemKind.Text;
        return new ClipboardRecord(
            Guid.NewGuid(),
            kind,
            text,
            source,
            copiedAt,
            false,
            false,
            true,
            null);
    }

    public sealed record WindowIdentity(int ProcessId, string ProcessName, string WindowTitle)
    {
        public static WindowIdentity FromHandle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return new WindowIdentity(0, string.Empty, string.Empty);
            }

            _ = GetWindowThreadProcessId(windowHandle, out var processId);
            var title = new char[512];
            var titleLength = GetWindowText(windowHandle, title, title.Length);
            try
            {
                using var process = Process.GetProcessById((int)processId);
                return new WindowIdentity(
                    (int)processId,
                    process.ProcessName + ".exe",
                    titleLength > 0 ? new string(title, 0, titleLength) : string.Empty);
            }
            catch (ArgumentException)
            {
                return new WindowIdentity((int)processId, string.Empty, string.Empty);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, char[] text, int maximumCount);
}
