using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Defines the contract for Chat Recording Service
/// </summary>
public interface IChatRecordingService
{
    /// <summary>
    /// Gets metadata path
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <returns>The resulting string</returns>
    string GetMetadataPath(string transcriptPath);

    /// <summary>
    /// Attempts to read metadata
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <returns>The resulting session recording metadata?</returns>
    SessionRecordingMetadata? TryReadMetadata(string transcriptPath);

    /// <summary>
    /// Executes refresh metadata async
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="context">The context</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to session recording metadata?</returns>
    Task<SessionRecordingMetadata?> RefreshMetadataAsync(
        string transcriptPath,
        SessionRecordingContext context,
        CancellationToken cancellationToken = default);
}
