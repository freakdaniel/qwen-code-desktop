namespace QwenCode.App.Models;

/// <summary>
/// Represents the Desktop Session Activity Summary
/// </summary>
public sealed class DesktopSessionActivitySummary
{
    /// <summary>
    /// Gets or sets the user count
    /// </summary>
    public required int UserCount { get; init; }

    /// <summary>
    /// Gets or sets the assistant count
    /// </summary>
    public required int AssistantCount { get; init; }

    /// <summary>
    /// Gets or sets the command count
    /// </summary>
    public required int CommandCount { get; init; }

    /// <summary>
    /// Gets or sets the tool count
    /// </summary>
    public required int ToolCount { get; init; }

    /// <summary>
    /// Gets or sets the pending approval count
    /// </summary>
    public required int PendingApprovalCount { get; init; }

    /// <summary>
    /// Gets or sets the pending question count
    /// </summary>
    public required int PendingQuestionCount { get; init; }

    /// <summary>
    /// Gets or sets the completed tool count
    /// </summary>
    public required int CompletedToolCount { get; init; }

    /// <summary>
    /// Gets or sets the failed tool count
    /// </summary>
    public required int FailedToolCount { get; init; }

    /// <summary>
    /// Gets or sets the last timestamp
    /// </summary>
    public string LastTimestamp { get; init; } = string.Empty;
}
