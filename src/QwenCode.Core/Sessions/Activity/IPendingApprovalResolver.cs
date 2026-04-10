using QwenCode.Core.Models;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Defines the contract for Pending Approval Resolver
/// </summary>
public interface IPendingApprovalResolver
{
    /// <summary>
    /// Resolves pending tool
    /// </summary>
    /// <param name="detail">The detail</param>
    /// <param name="entryId">The entry id</param>
    /// <returns>The resulting desktop session entry</returns>
    DesktopSessionEntry ResolvePendingTool(DesktopSessionDetail detail, string? entryId);

    /// <summary>
    /// Resolves pending question
    /// </summary>
    /// <param name="detail">The detail</param>
    /// <param name="entryId">The entry id</param>
    /// <returns>The resulting desktop session entry</returns>
    DesktopSessionEntry ResolvePendingQuestion(DesktopSessionDetail detail, string? entryId);
}
