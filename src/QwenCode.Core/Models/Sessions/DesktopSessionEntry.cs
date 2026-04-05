namespace QwenCode.App.Models;

/// <summary>
/// Represents the Desktop Session Entry
/// </summary>
public sealed class DesktopSessionEntry
{
    /// <summary>
    /// Gets or sets the id
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the type
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the timestamp
    /// </summary>
    public required string Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public required string GitBranch { get; init; }

    /// <summary>
    /// Gets or sets the title
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the body
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval state
    /// </summary>
    public string ApprovalState { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the exit code
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// Gets or sets the arguments
    /// </summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the source path
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the resolution status
    /// </summary>
    public string ResolutionStatus { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the resolved at
    /// </summary>
    public string ResolvedAt { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the changed files
    /// </summary>
    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the questions
    /// </summary>
    public IReadOnlyList<DesktopQuestionPrompt> Questions { get; init; } = [];

    /// <summary>
    /// Gets or sets the answers
    /// </summary>
    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
