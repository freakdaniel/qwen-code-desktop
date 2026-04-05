using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Defines the contract for Transcript Store
/// </summary>
public interface ITranscriptStore
{
    /// <summary>
    /// Lists sessions
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="limit">The limit</param>
    /// <returns>The resulting i read only list session preview</returns>
    IReadOnlyList<SessionPreview> ListSessions(WorkspacePaths paths, int limit = 24);

    /// <summary>
    /// Gets session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting desktop session detail?</returns>
    DesktopSessionDetail? GetSession(WorkspacePaths paths, GetDesktopSessionRequest request);
}
