using Microsoft.Extensions.Options;
using QwenCode.App.Followup;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Followup Projection Service
/// </summary>
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="followupSuggestionService">The followup suggestion service</param>
public sealed class FollowupProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IFollowupSuggestionService followupSuggestionService) : IDesktopFollowupProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    /// <summary>
    /// Gets suggestions async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to followup suggestion snapshot</returns>
    public Task<FollowupSuggestionSnapshot> GetSuggestionsAsync(
        GetFollowupSuggestionsRequest request,
        CancellationToken cancellationToken = default) =>
        followupSuggestionService.GetSuggestionsAsync(
            workspacePathResolver.Resolve(shellOptions.Workspace),
            request,
            cancellationToken);
}
