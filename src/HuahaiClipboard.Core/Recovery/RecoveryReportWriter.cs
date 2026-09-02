using System.Text;
using System.Text.Json;

namespace HuahaiClipboard.Core.Recovery;

/// <summary>
/// Emits local recovery evidence. The persisted projection intentionally omits
/// values supplied as sensitive so clipboard bodies and note markup cannot be
/// copied into the report by a caller.
/// </summary>
public sealed class RecoveryReportWriter
{
    public async Task<RecoveryReportResult> WriteAsync(
        RecoveryReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (string.IsNullOrWhiteSpace(report.ReportId))
        {
            throw new ArgumentException("Recovery report ID is required.", nameof(report));
        }

        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        var fileBase = $"recovery-report-{SanitizeFileName(report.ReportId)}";
        var jsonPath = Path.Combine(root, fileBase + ".json");
        var textPath = Path.Combine(root, fileBase + ".txt");
        var projection = CreateProjection(report);

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(projection, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await File.WriteAllTextAsync(textPath, CreateText(projection), cancellationToken);
        return new RecoveryReportResult(jsonPath, textPath);
    }

    private static RecoveryReportProjection CreateProjection(RecoveryReport report) =>
        new(
            report.ReportId,
            report.CreatedAt,
            report.Sources.Select(source => new RecoveryReportSourceProjection(
                source.Root,
                source.Provenance,
                source.State.ToString(),
                source.HistoryCount,
                source.TodoCount,
                source.NoteCount,
                source.ImageCount,
                source.FileHashes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                source.ErrorCode)).ToArray(),
            report.Conflicts.Select(conflict => new RecoveryConflictProjection(
                conflict.Kind.ToString(),
                conflict.OriginalId,
                conflict.RecoveredId)).ToArray());

    private static string CreateText(RecoveryReportProjection report)
    {
        var text = new StringBuilder();
        text.AppendLine("HuahaiClipboard Recovery Report");
        text.AppendLine($"Report ID: {report.ReportId}");
        text.AppendLine($"Created: {report.CreatedAt:O}");
        text.AppendLine($"Sources: {report.Sources.Length}");
        foreach (var source in report.Sources)
        {
            text.AppendLine($"- {source.Provenance}: {source.Root}");
            text.AppendLine($"  state={source.State}; history={source.HistoryCount}; todo={source.TodoCount}; note={source.NoteCount}; image={source.ImageCount}; error={source.ErrorCode ?? "none"}");
            foreach (var hash in source.FileHashes) text.AppendLine($"  hash={hash}");
        }

        text.AppendLine($"Conflicts: {report.Conflicts.Length}");
        foreach (var conflict in report.Conflicts)
        {
            text.AppendLine($"- {conflict.Kind}: {conflict.OriginalId} -> {conflict.RecoveredId}");
        }

        return text.ToString();
    }

    private static string SanitizeFileName(string value) =>
        new(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray());

    private sealed record RecoveryReportProjection(
        string ReportId,
        DateTimeOffset CreatedAt,
        RecoveryReportSourceProjection[] Sources,
        RecoveryConflictProjection[] Conflicts);

    private sealed record RecoveryReportSourceProjection(
        string Root,
        string Provenance,
        string State,
        int HistoryCount,
        int TodoCount,
        int NoteCount,
        int ImageCount,
        string[] FileHashes,
        string? ErrorCode);

    private sealed record RecoveryConflictProjection(string Kind, string OriginalId, string RecoveredId);
}
