using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Followup Projection Service
/// </summary>
public interface IDesktopFollowupProjectionService
{
    /// <summary>
    /// Gets suggestions async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to followup suggestion snapshot</returns>
    Task<FollowupSuggestionSnapshot> GetSuggestionsAsync(
        GetFollowupSuggestionsRequest request,
        CancellationToken cancellationToken = default);
}
