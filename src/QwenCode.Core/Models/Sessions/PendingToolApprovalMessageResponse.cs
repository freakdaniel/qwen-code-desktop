namespace QwenCode.App.Models;

/// <summary>
/// Represents the Pending Tool Approval Message Response
/// </summary>
public sealed class PendingToolApprovalMessageResponse
{
    /// <summary>
    /// Gets or sets the correlation id
    /// </summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the detail
    /// </summary>
    public required DesktopSessionDetail Detail { get; init; }

    /// <summary>
    /// Gets or sets the pending tool
    /// </summary>
    public required DesktopSessionEntry PendingTool { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public string GitBranch { get; init; } = string.Empty;
}
