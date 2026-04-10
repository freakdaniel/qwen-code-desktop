using Microsoft.Extensions.Options;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;
using QwenCode.Core.Runtime;

namespace QwenCode.Core.Followup;

/// <summary>
/// Represents the Provider Backed Followup Suggestion Generator
/// </summary>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="promptAssembler">The prompt assembler</param>
/// <param name="contentGenerator">The content generator</param>
/// <param name="options">The options</param>
public sealed class ProviderBackedFollowupSuggestionGenerator(
    QwenRuntimeProfileService runtimeProfileService,
    IAssistantPromptAssembler promptAssembler,
    IContentGenerator contentGenerator,
    IOptions<NativeAssistantRuntimeOptions> options) : IFollowupSuggestionGenerator
{
    private const string SuggestionPrompt = "Predict the most likely short next message the user would type in this conversation.";

    private readonly NativeAssistantRuntimeOptions runtimeOptions = options.Value;

    /// <summary>
    /// Generates async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="detail">The detail</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to followup suggestion?</returns>
    public async Task<FollowupSuggestion?> GenerateAsync(
        WorkspacePaths paths,
        DesktopSessionDetail detail,
        CancellationToken cancellationToken = default)
    {
        if (detail.Summary.AssistantCount < 1)
        {
            return null;
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var workingDirectory = string.IsNullOrWhiteSpace(detail.Session.WorkingDirectory)
            ? runtimeProfile.ProjectRoot
            : detail.Session.WorkingDirectory;
        var request = new AssistantTurnRequest
        {
            SessionId = detail.Session.SessionId,
            Prompt = SuggestionPrompt,
            WorkingDirectory = workingDirectory,
            TranscriptPath = detail.TranscriptPath,
            RuntimeProfile = runtimeProfile,
            GitBranch = detail.Session.GitBranch,
            ToolExecution = new NativeToolExecutionResult
            {
                ToolName = string.Empty,
                Status = "not-requested",
                ApprovalState = "allow",
                WorkingDirectory = workingDirectory,
                ChangedFiles = []
            },
            PromptMode = AssistantPromptMode.FollowupSuggestion,
            DisableTools = true
        };
        var promptContext = await promptAssembler.AssembleAsync(request, cancellationToken: cancellationToken);

        try
        {
            var response = await contentGenerator.GenerateContentAsync(
                new LlmContentRequest
                {
                    SessionId = request.SessionId,
                    Prompt = request.Prompt,
                    WorkingDirectory = request.WorkingDirectory,
                    TranscriptPath = request.TranscriptPath,
                    RuntimeProfile = request.RuntimeProfile,
                    PromptContext = promptContext,
                    GitBranch = request.GitBranch,
                    SystemPrompt = request.SystemPromptOverride,
                    PromptMode = request.PromptMode,
                    ModelOverride = request.ModelOverride,
                    AuthTypeOverride = request.AuthTypeOverride,
                    EndpointOverride = request.EndpointOverride,
                    ApiKeyOverride = request.ApiKeyOverride,
                    TemperatureOverride = runtimeOptions.Temperature,
                    DisableTools = true
                },
                cancellationToken);
            if (response is null ||
                !string.IsNullOrWhiteSpace(response.StopReason) &&
                !string.Equals(response.StopReason, "completed", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(response.ProviderName, "fallback", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalized = FollowupSuggestionFilter.Normalize(response.Content);
            var filterReason = FollowupSuggestionFilter.GetFilterReason(normalized);
            if (!string.IsNullOrWhiteSpace(filterReason))
            {
                return null;
            }

            return new FollowupSuggestion
            {
                Text = normalized,
                Kind = "predicted-next-step",
                Source = response.ProviderName,
                Confidence = 90
            };
        }
        catch
        {
            // Ignore provider failures and fall back to heuristic suggestions.
            return null;
        }
    }
}
