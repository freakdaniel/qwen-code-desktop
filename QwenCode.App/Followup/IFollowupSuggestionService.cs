using QwenCode.App.Models;

namespace QwenCode.App.Followup;

public interface IFollowupSuggestionService
{
    Task<FollowupSuggestionSnapshot> GetSuggestionsAsync(
        WorkspacePaths paths,
        GetFollowupSuggestionsRequest request,
        CancellationToken cancellationToken = default);
}
