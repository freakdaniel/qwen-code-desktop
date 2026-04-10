namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Assistant Tool Call
/// </summary>
public sealed class AssistantToolCall
{
    /// <summary>
    /// Gets or sets the id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the arguments json
    /// </summary>
    public string ArgumentsJson { get; init; } = "{}";
}
