namespace QwenCode.App.Models;

public sealed class DesktopSessionDetail
{
    public required SessionPreview Session { get; init; }

    public required string TranscriptPath { get; init; }

    public required int EntryCount { get; init; }

    public required DesktopSessionActivitySummary Summary { get; init; }

    public required IReadOnlyList<DesktopSessionEntry> Entries { get; init; }
}
