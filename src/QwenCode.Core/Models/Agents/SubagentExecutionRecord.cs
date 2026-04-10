using QwenCode.Core.Runtime;

namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Subagent Execution Record
/// </summary>
public sealed class SubagentExecutionRecord
{
    /// <summary>
    /// Gets or sets the execution id
    /// </summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the linked orchestration task id
    /// </summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the report
    /// </summary>
    public string Report { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stop reason
    /// </summary>
    public string StopReason { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stats
    /// </summary>
    public AssistantExecutionStats Stats { get; init; } = new();

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public string TranscriptPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the allowed tools
    /// </summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>
    /// Gets or sets the tool executions
    /// </summary>
    public IReadOnlyList<SubagentToolExecutionRecord> ToolExecutions { get; init; } = [];

    /// <summary>
    /// Gets or sets the started at utc
    /// </summary>
    public DateTime StartedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the ended at utc
    /// </summary>
    public DateTime EndedAtUtc { get; init; }

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public DateTime TimestampUtc { get; init; }
}
