namespace QwenCode.App.Runtime;

internal sealed record NativeAssistantPromptCompositionContext(
    AssistantTurnRequest Request,
    AssistantPromptContext PromptContext,
    string RuntimeInstructionPrompt = "",
    string RequestSpecificSystemPrompt = "",
    string ModelId = "",
    string ProviderFlavor = "")
{
    public bool IsApprovalResolution => Request.IsApprovalResolution;

    public AssistantPromptMode PromptMode => Request.PromptMode;

    public bool AreToolsDisabled => Request.DisableTools;

    public bool HasAllowedToolList => Request.AllowedToolNames.Count > 0;

    public bool HasProjectSummary => PromptContext.ProjectSummary is { HasHistory: true };

    public bool IsPlanMode => Request.PromptMode == AssistantPromptMode.Plan;

    public bool IsFollowupSuggestion => Request.PromptMode == AssistantPromptMode.FollowupSuggestion;

    public bool IsSubagent => Request.PromptMode == AssistantPromptMode.Subagent;

    public bool IsArenaCompetitor => Request.PromptMode == AssistantPromptMode.ArenaCompetitor;

    public bool HasRuntimeInstructions =>
        !string.IsNullOrWhiteSpace(RuntimeInstructionPrompt) &&
        !string.Equals(RuntimeInstructionPrompt, NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt, StringComparison.Ordinal);

    public bool HasRequestSpecificInstructions =>
        !string.IsNullOrWhiteSpace(RequestSpecificSystemPrompt) &&
        !string.Equals(RequestSpecificSystemPrompt, NativeAssistantRuntimePromptBuilder.DefaultSystemPrompt, StringComparison.Ordinal) &&
        !string.Equals(RequestSpecificSystemPrompt, RuntimeInstructionPrompt, StringComparison.Ordinal);

    public bool HasCustomInstructions => HasRuntimeInstructions || HasRequestSpecificInstructions;

    public bool CanUseTool(string toolName)
    {
        if (AreToolsDisabled || string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return Request.AllowedToolNames.Count == 0 ||
               Request.AllowedToolNames.Contains(toolName, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasModelSpecificToolGuidance =>
        !AreToolsDisabled &&
        !string.IsNullOrWhiteSpace(ModelId);
}
