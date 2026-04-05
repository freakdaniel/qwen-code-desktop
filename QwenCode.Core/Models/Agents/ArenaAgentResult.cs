using QwenCode.App.Runtime;

namespace QwenCode.App.Models;

public sealed class ArenaAgentResult
{
    public string AgentId { get; init; } = string.Empty;

    public string AgentName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string StopReason { get; init; } = string.Empty;

    public AssistantExecutionStats Stats { get; init; } = new();

    public string WorktreePath { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string TranscriptPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public string Diff { get; init; } = string.Empty;

    public IReadOnlyList<string> ModifiedFiles { get; init; } = [];

    public IReadOnlyList<SubagentToolExecutionRecord> ToolExecutions { get; init; } = [];

    public DateTime StartedAtUtc { get; init; }

    public DateTime EndedAtUtc { get; init; }
}
