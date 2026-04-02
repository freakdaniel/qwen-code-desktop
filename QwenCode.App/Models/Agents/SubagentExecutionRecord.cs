namespace QwenCode.App.Models;

public sealed class SubagentExecutionRecord
{
    public string ExecutionId { get; init; } = string.Empty;

    public string AgentName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Report { get; init; } = string.Empty;

    public DateTime TimestampUtc { get; init; }
}
