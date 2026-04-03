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

    public string ProviderName { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string TranscriptPath { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    public IReadOnlyList<SubagentToolExecutionRecord> ToolExecutions { get; init; } = [];

    public DateTime TimestampUtc { get; init; }
}
