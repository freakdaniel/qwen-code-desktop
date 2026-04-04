using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopFollowupProjectionService
{
    Task<FollowupSuggestionSnapshot> GetSuggestionsAsync(
        GetFollowupSuggestionsRequest request,
        CancellationToken cancellationToken = default);
}
