namespace QwenCode.App.Models;

public sealed class SessionExportSnapshot
{
    public required SessionPreview Session { get; init; }

    public required string TranscriptPath { get; init; }

    public int EntryCount { get; init; }

    public int WindowOffset { get; init; }

    public int WindowSize { get; init; }

    public required DesktopSessionActivitySummary Summary { get; init; }

    public required IReadOnlyList<DesktopSessionEntry> Entries { get; init; }

    public DateTime ExportedAtUtc { get; init; }
}
