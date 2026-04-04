using Microsoft.Extensions.Options;
using QwenCode.App.Followup;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

public sealed class FollowupProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IFollowupSuggestionService followupSuggestionService) : IDesktopFollowupProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    public Task<FollowupSuggestionSnapshot> GetSuggestionsAsync(
        GetFollowupSuggestionsRequest request,
        CancellationToken cancellationToken = default) =>
        followupSuggestionService.GetSuggestionsAsync(
            workspacePathResolver.Resolve(shellOptions.Workspace),
            request,
            cancellationToken);
}
