namespace QwenCode.App.Models;

public sealed class DesktopSessionEntry
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Timestamp { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string GitBranch { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    public string Status { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string ApprovalState { get; init; } = string.Empty;

    public int? ExitCode { get; init; }

    public string Arguments { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;

    public string ResolutionStatus { get; init; } = string.Empty;

    public string ResolvedAt { get; init; } = string.Empty;

    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    public IReadOnlyList<DesktopQuestionPrompt> Questions { get; init; } = [];

    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
