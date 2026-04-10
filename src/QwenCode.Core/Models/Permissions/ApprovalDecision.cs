namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Approval Decision
/// </summary>
public sealed class ApprovalDecision
{
    /// <summary>
    /// Gets or sets the state
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets or sets the reason
    /// </summary>
    public required string Reason { get; init; }
}
