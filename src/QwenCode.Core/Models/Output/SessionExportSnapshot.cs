namespace QwenCode.App.Models;

/// <summary>
/// Represents the Session Export Snapshot
/// </summary>
public sealed class SessionExportSnapshot
{
    /// <summary>
    /// Gets or sets the session
    /// </summary>
    public required SessionPreview Session { get; init; }

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public required string TranscriptPath { get; init; }

    /// <summary>
    /// Gets or sets the entry count
    /// </summary>
    public int EntryCount { get; init; }

    /// <summary>
    /// Gets or sets the window offset
    /// </summary>
    public int WindowOffset { get; init; }

    /// <summary>
    /// Gets or sets the window size
    /// </summary>
    public int WindowSize { get; init; }

    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required DesktopSessionActivitySummary Summary { get; init; }

    /// <summary>
    /// Gets or sets the entries
    /// </summary>
    public required IReadOnlyList<DesktopSessionEntry> Entries { get; init; }

    /// <summary>
    /// Gets or sets the exported at utc
    /// </summary>
    public DateTime ExportedAtUtc { get; init; }
}
