namespace QwenCode.App.Models;

public sealed class DesktopSessionActivitySummary
{
    public required int UserCount { get; init; }

    public required int AssistantCount { get; init; }

    public required int CommandCount { get; init; }

    public required int ToolCount { get; init; }

    public required int PendingApprovalCount { get; init; }

    public required int CompletedToolCount { get; init; }

    public required int FailedToolCount { get; init; }

    public string LastTimestamp { get; init; } = string.Empty;
}
