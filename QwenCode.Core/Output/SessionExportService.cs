using QwenCode.App.Models;
using QwenCode.App.Sessions;

namespace QwenCode.App.Output;

public sealed class SessionExportService(
    ITranscriptStore transcriptStore,
    IOutputFormatter outputFormatter) : ISessionExportService
{
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

    public string FormatSession(WorkspacePaths paths, GetDesktopSessionRequest request, OutputFormat format)
    {
        var snapshot = BuildSessionSnapshot(paths, request);
        return snapshot is null
            ? string.Empty
            : outputFormatter.Format(snapshot, format);
    }
}
