using QwenCode.Core.Models;

namespace QwenCode.Core.Followup;

/// <summary>
/// Defines the contract for Followup Suggestion Generator
/// </summary>
public interface IFollowupSuggestionGenerator
{
    /// <summary>
    /// Generates async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="detail">The detail</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to followup suggestion?</returns>
    Task<FollowupSuggestion?> GenerateAsync(
        WorkspacePaths paths,
        DesktopSessionDetail detail,
        CancellationToken cancellationToken = default);
}
