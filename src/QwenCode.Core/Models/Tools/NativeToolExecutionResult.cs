namespace QwenCode.App.Models;

/// <summary>
/// Represents the Native Tool Execution Result
/// </summary>
public sealed class NativeToolExecutionResult
{
    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets or sets the approval state
    /// </summary>
    public required string ApprovalState { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the output
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the exit code
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets or sets the changed files
    /// </summary>
    public required IReadOnlyList<string> ChangedFiles { get; init; }

    /// <summary>
    /// Gets or sets the questions
    /// </summary>
    public IReadOnlyList<DesktopQuestionPrompt> Questions { get; init; } = [];

    /// <summary>
    /// Gets or sets the answers
    /// </summary>
    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
