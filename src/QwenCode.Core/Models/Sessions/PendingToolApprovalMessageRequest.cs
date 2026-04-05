namespace QwenCode.App.Models;

/// <summary>
/// Represents the Pending Tool Approval Message Request
/// </summary>
public sealed class PendingToolApprovalMessageRequest
{
    /// <summary>
    /// Gets or sets the correlation id
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the paths
    /// </summary>
    public required WorkspacePaths Paths { get; init; }

    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the entry id
    /// </summary>
    public string EntryId { get; init; } = string.Empty;
}
