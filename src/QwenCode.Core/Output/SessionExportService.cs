using QwenCode.App.Models;
using QwenCode.App.Sessions;

namespace QwenCode.App.Output;

/// <summary>
/// Represents the Session Export Service
/// </summary>
/// <param name="transcriptStore">The transcript store</param>
/// <param name="outputFormatter">The output formatter</param>
public sealed class SessionExportService(
    ITranscriptStore transcriptStore,
    IOutputFormatter outputFormatter) : ISessionExportService
{
    /// <summary>
    /// Builds session snapshot
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting session export snapshot?</returns>
    public SessionExportSnapshot? BuildSessionSnapshot(WorkspacePaths paths, GetDesktopSessionRequest request)
    {
        var detail = transcriptStore.GetSession(paths, request);
        if (detail is null)
        {
            return null;
        }

        return new SessionExportSnapshot
        {
            Session = detail.Session,
            TranscriptPath = detail.TranscriptPath,
            EntryCount = detail.EntryCount,
            WindowOffset = detail.WindowOffset,
            WindowSize = detail.WindowSize,
            Summary = detail.Summary,
            Entries = detail.Entries,
            ExportedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Executes format session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="format">The format</param>
    /// <returns>The resulting string</returns>
    public string FormatSession(WorkspacePaths paths, GetDesktopSessionRequest request, OutputFormat format)
    {
        var snapshot = BuildSessionSnapshot(paths, request);
        return snapshot is null
            ? string.Empty
            : outputFormatter.Format(snapshot, format);
    }
}
