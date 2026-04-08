using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

internal static class NativeAssistantRuntimePromptBuilder
{
    public const string SystemPromptDynamicBoundary = "__SYSTEM_PROMPT_DYNAMIC_BOUNDARY__";

    public static string DefaultSystemPrompt { get; } = ComposeDefaultSystemPrompt();

    public static string BuildSystemPrompt(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        string runtimeInstructionPrompt,
        string requestSpecificSystemPrompt,
        string modelId = "",
        string providerFlavor = "")
    {
        var compositionContext = new NativeAssistantPromptCompositionContext(
            request,
            promptContext,
            runtimeInstructionPrompt,
            requestSpecificSystemPrompt,
            modelId,
            providerFlavor);
        var staticPrefix = BuildStaticSystemPromptPrefix(compositionContext);
        var dynamicTail = BuildDynamicSystemPromptTail(compositionContext);
        return string.IsNullOrWhiteSpace(dynamicTail)
            ? staticPrefix
            : $"{staticPrefix}{Environment.NewLine}{Environment.NewLine}{dynamicTail}";
    }

    public static string BuildCurrentTurnUserMessage(
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        IReadOnlyList<AssistantToolCallResult> toolHistory)
    {
        var allowedTools = request.DisableTools
            ? "Tools are disabled for this request."
            : request.AllowedToolNames.Count == 0
                ? "Use any available native tool when it helps."
                : $"Allowed native tools: {string.Join(", ", request.AllowedToolNames)}";
        var toolLoopGuidance = toolHistory.Count == 0
            ? "No tool results have been recorded in this turn yet. Either answer directly or start the first tool call."
            : "Tool results for this turn are attached below as assistant/tool messages. Continue from them instead of restarting the task.";
        var historyHighlights = promptContext.HistoryHighlights.Count == 0
            ? "- No prior transcript highlights were retained for this turn."
            : string.Join(Environment.NewLine, promptContext.HistoryHighlights.Select(static item => $"- {item}"));
        var contextFiles = promptContext.ContextFiles.Count == 0
            ? "- No workspace context files were included."
            : string.Join(Environment.NewLine + Environment.NewLine, promptContext.ContextFiles);

        return $$"""
Current user task:
{{request.Prompt}}

Turn constraints:
- {{allowedTools}}
- Approval resolution turn: {{request.IsApprovalResolution}}
- Current provider round already has {{toolHistory.Count}} completed tool call result(s).

History highlights:
{{historyHighlights}}

Workspace context files:
{{contextFiles}}

Tool loop guidance:
- {{toolLoopGuidance}}
- Use tools when they materially reduce uncertainty or are required to complete the task.
- If the task is finished, answer directly.
""";
    }

    private static string ComposeDefaultSystemPrompt()
    {
        var emptyContext = new NativeAssistantPromptCompositionContext(
            new AssistantTurnRequest
            {
                SessionId = string.Empty,
                Prompt = string.Empty,
                WorkingDirectory = string.Empty,
                TranscriptPath = string.Empty,
                RuntimeProfile = new QwenRuntimeProfile
                {
                    ProjectRoot = string.Empty,
                    GlobalQwenDirectory = string.Empty,
                    RuntimeBaseDirectory = string.Empty,
                    RuntimeSource = string.Empty,
                    ProjectDataDirectory = string.Empty,
                    ChatsDirectory = string.Empty,
                    HistoryDirectory = string.Empty,
                    ContextFileNames = [],
                    ContextFilePaths = [],
                    ApprovalProfile = new ApprovalProfile
                    {
                        DefaultMode = "default",
                        AllowRules = [],
                        AskRules = [],
                        DenyRules = []
                    }
                },
                ToolExecution = new NativeToolExecutionResult
                {
                    ToolName = string.Empty,
                    Status = "not-requested",
                    ApprovalState = "allow",
                    WorkingDirectory = string.Empty,
                    ChangedFiles = []
                }
            },
            new AssistantPromptContext
            {
                Messages = [],
                ContextFiles = [],
                HistoryHighlights = [],
                DurableMemorySummary = string.Empty,
                UserInstructionSummary = string.Empty,
                WorkspaceInstructionSummary = string.Empty,
                McpServerSummary = string.Empty,
                ScratchpadSummary = string.Empty,
                LanguageSummary = string.Empty,
                OutputStyleSummary = string.Empty
            },
            string.Empty,
            string.Empty);

        return BuildStaticSystemPromptPrefix(emptyContext);
    }

    public static string BuildStaticSystemPromptPrefix(NativeAssistantPromptCompositionContext context) =>
        ComposeSections(
            NativeAssistantPromptSectionResolver.ResolveStaticSystemSections(context),
            context);

    public static string BuildDynamicSystemPromptTail(NativeAssistantPromptCompositionContext context) =>
        ComposeSections(
            NativeAssistantPromptSectionResolver.ResolveDynamicSystemSections(context),
            context);

    private static string ComposeSections(
        IEnumerable<NativeAssistantPromptSection> sections,
        NativeAssistantPromptCompositionContext context) =>
        string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            sections
                .Select(section => section.Compute(context)?.Trim())
                .Where(static section => !string.IsNullOrWhiteSpace(section)));
}
