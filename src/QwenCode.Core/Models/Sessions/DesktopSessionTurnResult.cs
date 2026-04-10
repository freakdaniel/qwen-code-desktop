namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Desktop Session Turn Result
/// </summary>
public sealed class DesktopSessionTurnResult
{
    /// <summary>
    /// Gets or sets the session
    /// </summary>
    public required SessionPreview Session { get; init; }

    /// <summary>
    /// Gets or sets the assistant summary
    /// </summary>
    public required string AssistantSummary { get; init; }

    /// <summary>
    /// Gets or sets the created new session
    /// </summary>
    public required bool CreatedNewSession { get; init; }

    /// <summary>
    /// Gets or sets the tool execution
    /// </summary>
    public required NativeToolExecutionResult ToolExecution { get; init; }

    /// <summary>
    /// Gets or sets the resolved command
    /// </summary>
    public ResolvedCommand? ResolvedCommand { get; init; }
}
