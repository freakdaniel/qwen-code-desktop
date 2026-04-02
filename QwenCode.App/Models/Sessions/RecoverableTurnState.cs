namespace QwenCode.App.Models;

public sealed class RecoverableTurnState
{
    public required string SessionId { get; init; }

    public required string Prompt { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string GitBranch { get; init; }

    public required string RecoveryReason { get; init; }

    public required DateTime LastUpdatedAtUtc { get; init; }

    public string ContentSnapshot { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;
}
