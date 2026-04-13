using QwenCode.Core.Models;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Defines the contract for Session Service
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Executes session exists
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool SessionExists(WorkspacePaths paths, string sessionId);

    /// <summary>
    /// Loads last session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting session preview?</returns>
    SessionPreview? LoadLastSession(WorkspacePaths paths);

    /// <summary>
    /// Loads conversation
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>The resulting session conversation record?</returns>
    SessionConversationRecord? LoadConversation(WorkspacePaths paths, string sessionId);

    /// <summary>
    /// Removes session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool RemoveSession(WorkspacePaths paths, string sessionId);

    /// <summary>
    /// Renames session
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="title">The new title</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool RenameSession(WorkspacePaths paths, string sessionId, string title);
}
