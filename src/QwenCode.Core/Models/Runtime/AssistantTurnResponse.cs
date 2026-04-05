namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Assistant Turn Response
/// </summary>
public sealed class AssistantTurnResponse
{
    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public required string ProviderName { get; init; }

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
    /// Gets or sets the tool calls
    /// </summary>
    public IReadOnlyList<AssistantToolCall> ToolCalls { get; init; } = [];

    /// <summary>
    /// Gets or sets the tool executions
    /// </summary>
    public IReadOnlyList<AssistantToolCallResult> ToolExecutions { get; init; } = [];
}
