using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.App.Infrastructure.Mocks;

public sealed class MockClipboardHistorySource : IClipboardHistorySource
{
    private static readonly DateTimeOffset AnchorTime =
        new(2026, 8, 4, 9, 0, 0, TimeSpan.FromHours(8));

    private readonly object syncRoot = new();
    private readonly List<ClipboardRecord> records = CreateRecords();

    public Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            return Task.FromResult<IReadOnlyList<ClipboardRecord>>(records.ToArray());
        }
    }

    public Task<ClipboardRecord?> FindAsync(Guid recordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            return Task.FromResult(records.FirstOrDefault(record => record.Id == recordId));
        }
    }

    public Task UpsertAsync(ClipboardRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(record);
        lock (syncRoot)
        {
            records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task SetFavoriteAsync(Guid recordId, bool value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateRecord(recordId, record => record with { IsFavorite = value });
        return Task.CompletedTask;
    }

    public Task SetPinnedAsync(Guid recordId, bool value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateRecord(recordId, record => record with { IsPinned = value });
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid recordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            var index = FindRecordIndex(recordId);
            records.RemoveAt(index);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (syncRoot)
        {
            records.Clear();
        }

        return Task.CompletedTask;
    }

    public Task ClearUnprotectedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            records.RemoveAll(record => !record.IsFavorite && !record.IsPinned);
        }

        return Task.CompletedTask;
    }

    public Task PruneAsync(
        DateTimeOffset cutoff,
        bool preserveProtected,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            records.RemoveAll(record =>
                record.LastCopiedAt < cutoff &&
                (!preserveProtected || !record.IsFavorite && !record.IsPinned));
        }

        return Task.CompletedTask;
    }

    private void UpdateRecord(Guid recordId, Func<ClipboardRecord, ClipboardRecord> update)
    {
        lock (syncRoot)
        {
            var index = FindRecordIndex(recordId);
            records[index] = update(records[index]);
        }
    }

    private int FindRecordIndex(Guid recordId)
    {
        var index = records.FindIndex(record => record.Id == recordId);
        return index >= 0
            ? index
            : throw new KeyNotFoundException($"Clipboard record '{recordId}' does not exist.");
    }

    private static List<ClipboardRecord> CreateRecords() =>
    [
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            ClipboardItemKind.Text,
            "花海剪贴板让常用内容触手可及",
            "刚刚复制",
            AnchorTime,
            IsFavorite: true,
            IsPinned: true,
            IsAvailable: true,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            ClipboardItemKind.Link,
            "OpenAI 开发者文档",
            "https://platform.openai.com/docs",
            AnchorTime.AddMinutes(-2),
            IsFavorite: true,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            ClipboardItemKind.Image,
            "花海界面参考图",
            "1920 x 1080 PNG",
            AnchorTime.AddMinutes(-5),
            IsFavorite: false,
            IsPinned: true,
            IsAvailable: true,
            PreviewAssetPath: "Assets/Mocks/flower-sea-preview.png"),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            ClipboardItemKind.File,
            @"C:\资料\花海.txt",
            "文件不可用",
            AnchorTime.AddMinutes(-9),
            IsFavorite: true,
            IsPinned: true,
            IsAvailable: false,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000005"),
            ClipboardItemKind.Text,
            "Ctrl+Shift+V",
            "快捷键备忘",
            AnchorTime.AddMinutes(-14),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000006"),
            ClipboardItemKind.Link,
            "花海设计规范",
            "https://example.invalid/huahai-design",
            AnchorTime.AddMinutes(-21),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000007"),
            ClipboardItemKind.Image,
            "剪贴板动效关键帧",
            "1280 x 720 JPG",
            AnchorTime.AddMinutes(-34),
            IsFavorite: true,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: "Assets/Mocks/motion-keyframe-preview.jpg"),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000008"),
            ClipboardItemKind.File,
            @"C:\项目\花海\验收清单.docx",
            "Word 文档 · 28 KB",
            AnchorTime.AddMinutes(-47),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000009"),
            ClipboardItemKind.Text,
            "已复制内容会在此处按时间排序",
            "纯文本 · 17 字",
            AnchorTime.AddHours(-1),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000010"),
            ClipboardItemKind.Link,
            "Windows App SDK",
            "https://learn.microsoft.com/windows/apps/windows-app-sdk/",
            AnchorTime.AddHours(-2),
            IsFavorite: false,
            IsPinned: true,
            IsAvailable: true,
            PreviewAssetPath: null),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000011"),
            ClipboardItemKind.Image,
            "五主题并排预览",
            "2560 x 1440 PNG",
            AnchorTime.AddHours(-3),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: "Assets/Mocks/five-themes-preview.png"),
        new(
            Guid.Parse("00000000-0000-0000-0000-000000000012"),
            ClipboardItemKind.File,
            @"C:\资料\待手动粘贴.pdf",
            "前台应用不支持自动粘贴",
            AnchorTime.AddHours(-4),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: null)
    ];
}
