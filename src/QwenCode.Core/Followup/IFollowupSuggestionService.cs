using QwenCode.Core.Models;

namespace QwenCode.Core.Followup;

/// <summary>
/// Defines the contract for Followup Suggestion Service
/// </summary>
public interface IFollowupSuggestionService
{
    /// <summary>
    /// Gets suggestions async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to followup suggestion snapshot</returns>
    Task<FollowupSuggestionSnapshot> GetSuggestionsAsync(
        WorkspacePaths paths,
        GetFollowupSuggestionsRequest request,
        CancellationToken cancellationToken = default);
}
