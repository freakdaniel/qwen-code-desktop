using Microsoft.Extensions.Options;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Runtime;

namespace QwenCode.App.Followup;

public sealed class ProviderBackedFollowupSuggestionGenerator(
    QwenRuntimeProfileService runtimeProfileService,
    IAssistantPromptAssembler promptAssembler,
    IEnumerable<IAssistantResponseProvider> providers,
    IOptions<NativeAssistantRuntimeOptions> options) : IFollowupSuggestionGenerator
{
    private const string SuggestionPrompt = """
[SUGGESTION MODE: Suggest what the user might naturally type next.]

FIRST: Look at the user's recent messages and original request.

Your job is to predict what THEY would type next, not what you think they should do.

THE TEST: Would they think "I was just about to type that"?

Be specific: "run the tests" beats "continue".

NEVER SUGGEST:
- evaluative text like "looks good" or "thanks"
- questions
- AI-voice like "Let me..." or "I'll..."
- multiple sentences
- brand new ideas the user did not ask for

Stay silent if the next step is not obvious.

Reply with ONLY the suggestion, no quotes or explanation.
""";

    private readonly IReadOnlyList<IAssistantResponseProvider> providerChain = providers.ToArray();
    private readonly NativeAssistantRuntimeOptions runtimeOptions = options.Value;

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
            SystemPromptOverride = "You generate one short next-step suggestion for the user. Never call tools.",
            DisableTools = true
        };
        var promptContext = await promptAssembler.AssembleAsync(request, cancellationToken);

        foreach (var provider in BuildProviderChain())
        {
            try
            {
                var response = await provider.TryGenerateAsync(
                    request,
                    promptContext,
                    [],
                    runtimeOptions,
                    cancellationToken: cancellationToken);
                if (response is null ||
                    !string.IsNullOrWhiteSpace(response.StopReason) &&
                    !string.Equals(response.StopReason, "completed", StringComparison.OrdinalIgnoreCase) ||
                    response.ToolCalls.Count > 0)
                {
                    continue;
                }

                if (string.Equals(response.ProviderName, "fallback", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normalized = FollowupSuggestionFilter.Normalize(response.Summary);
                var filterReason = FollowupSuggestionFilter.GetFilterReason(normalized);
                if (!string.IsNullOrWhiteSpace(filterReason))
                {
                    continue;
                }

                return new FollowupSuggestion
                {
                    Text = normalized,
                    Kind = "predicted-next-step",
                    Source = provider.Name,
                    Confidence = 90
                };
            }
            catch
            {
                // Ignore provider failures and fall back to heuristic suggestions.
            }
        }

        return null;
    }

    private IReadOnlyList<IAssistantResponseProvider> BuildProviderChain()
    {
        var preferred = providerChain.FirstOrDefault(provider =>
            string.Equals(provider.Name, runtimeOptions.Provider, StringComparison.OrdinalIgnoreCase));
        var candidates = new List<IAssistantResponseProvider>();

        if (preferred is not null &&
            !string.Equals(preferred.Name, "fallback", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(preferred);
        }

        foreach (var provider in providerChain)
        {
            if (string.Equals(provider.Name, "fallback", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!candidates.Contains(provider))
            {
                candidates.Add(provider);
            }
        }

        return candidates;
    }
}
