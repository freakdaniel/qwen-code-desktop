namespace QwenCode.App.Models;

/// <summary>
/// Represents the Pending Question Answer Message Response
/// </summary>
public sealed class PendingQuestionAnswerMessageResponse
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
    /// Gets or sets the pending question
    /// </summary>
    public required DesktopSessionEntry PendingQuestion { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public string GitBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the questions
    /// </summary>
    public IReadOnlyList<DesktopQuestionPrompt> Questions { get; init; } = [];

    /// <summary>
    /// Gets or sets the answers
    /// </summary>
    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
