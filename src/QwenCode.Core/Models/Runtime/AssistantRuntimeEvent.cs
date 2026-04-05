namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Assistant Runtime Event
/// </summary>
public sealed class AssistantRuntimeEvent
{
    /// <summary>
    /// Gets or sets the stage
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the provider name
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the content delta
    /// </summary>
    public string ContentDelta { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the content snapshot
    /// </summary>
    public string ContentSnapshot { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name
    /// </summary>
    public string AgentName { get; init; } = string.Empty;
}
