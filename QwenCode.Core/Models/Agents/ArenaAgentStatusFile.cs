using QwenCode.App.Runtime;

namespace QwenCode.App.Models;

public sealed class ArenaAgentStatusFile
{
    public string AgentId { get; init; } = string.Empty;

    public string AgentName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string WorktreeName { get; init; } = string.Empty;

    public string WorktreePath { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string ProviderName { get; init; } = string.Empty;

    public string StopReason { get; init; } = string.Empty;

    public AssistantExecutionStats Stats { get; init; } = new();

    public string CurrentActivity { get; init; } = string.Empty;

    public string FinalSummary { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;

    public DateTime UpdatedAtUtc { get; init; }
}
