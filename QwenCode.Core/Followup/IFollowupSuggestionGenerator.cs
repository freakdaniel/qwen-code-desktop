using QwenCode.App.Models;

namespace QwenCode.App.Followup;

public interface IFollowupSuggestionGenerator
{
    Task<FollowupSuggestion?> GenerateAsync(
        WorkspacePaths paths,
        DesktopSessionDetail detail,
        CancellationToken cancellationToken = default);
}
