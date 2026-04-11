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

    /// <summary>
    /// Gets or sets the matched rule that produced the decision.
    /// </summary>
    public string MatchedRule { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the decision was produced by an explicit ask rule.
    /// </summary>
    public bool IsExplicitAskRule { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the decision denies the whole tool at registry level.
    /// </summary>
    public bool IsWholeToolDenyRule { get; init; }
}
