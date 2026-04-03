using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class AssistantTurnRequest
{
    public required string SessionId { get; init; }

    public required string Prompt { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string TranscriptPath { get; init; }

    public required QwenRuntimeProfile RuntimeProfile { get; init; }

    public string GitBranch { get; init; } = string.Empty;

    public CommandInvocationResult? CommandInvocation { get; init; }

    public ResolvedCommand? ResolvedCommand { get; init; }

    public required NativeToolExecutionResult ToolExecution { get; init; }

    public bool IsApprovalResolution { get; init; }

    public string SystemPromptOverride { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedToolNames { get; init; } = [];
}
