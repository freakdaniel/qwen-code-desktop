namespace QwenCode.App.Models;

public sealed class PendingToolApprovalMessageResponse
{
    public string CorrelationId { get; init; } = string.Empty;

    public required DesktopSessionDetail Detail { get; init; }

    public required DesktopSessionEntry PendingTool { get; init; }

    public string WorkingDirectory { get; init; } = string.Empty;

    public string GitBranch { get; init; } = string.Empty;
}
