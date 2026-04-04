using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface IChatRecordingService
{
    string GetMetadataPath(string transcriptPath);

    SessionRecordingMetadata? TryReadMetadata(string transcriptPath);

    Task<SessionRecordingMetadata?> RefreshMetadataAsync(
        string transcriptPath,
        SessionRecordingContext context,
        CancellationToken cancellationToken = default);
}
