namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Desktop Session Detail
/// </summary>
public sealed class DesktopSessionDetail
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
    public required int EntryCount { get; init; }

    /// <summary>
    /// Gets or sets the window offset
    /// </summary>
    public required int WindowOffset { get; init; }

    /// <summary>
    /// Gets or sets the window size
    /// </summary>
    public required int WindowSize { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has older entries
    /// </summary>
    public required bool HasOlderEntries { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has newer entries
    /// </summary>
    public required bool HasNewerEntries { get; init; }

    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required DesktopSessionActivitySummary Summary { get; init; }

    /// <summary>
    /// Gets or sets the entries
    /// </summary>
    public required IReadOnlyList<DesktopSessionEntry> Entries { get; init; }
}
