using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class RecoveryReportWriterTests
{
    [TestMethod]
    public async Task WriteAsync_ExcludesClipboardBodiesAndNoteHtmlFromBothReportFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-report-{Guid.NewGuid():N}");
        try
        {
            var report = new RecoveryReport(
                "report-1",
                DateTimeOffset.Parse("2026-09-02T10:00:00+08:00"),
                [new RecoveryReportSource(
                    @"F:\\HuahaiClipboard\\Data\\S-1",
                    "legacy local app data",
                    RecoveryInspectionState.Readable,
                    2,
                    1,
                    1,
                    1,
                    ["history.dat=ABC123"],
                    null)],
                [new RecoveryConflict(RecoveryConflictKind.NoteContent, "note-1", "note-2")],
                ["clipboard-body", "<p>private note html</p>"]);

            var result = await new RecoveryReportWriter().WriteAsync(report, root, CancellationToken.None);
            var json = await File.ReadAllTextAsync(result.JsonPath);
            var text = await File.ReadAllTextAsync(result.TextPath);

            Assert.IsFalse(json.Contains("clipboard-body", StringComparison.Ordinal));
            Assert.IsFalse(json.Contains("private note html", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("clipboard-body", StringComparison.Ordinal));
            Assert.IsFalse(text.Contains("private note html", StringComparison.Ordinal));
            StringAssert.Contains(json, "legacy local app data");
            StringAssert.Contains(text, "note-1");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
