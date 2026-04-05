namespace QwenCode.App.Models;

public sealed class NativeToolExecutionResult
{
    public required string ToolName { get; init; }

    public required string Status { get; init; }

    public required string ApprovalState { get; init; }

    public required string WorkingDirectory { get; init; }

    public string Output { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public int ExitCode { get; init; }

    public required IReadOnlyList<string> ChangedFiles { get; init; }

    public IReadOnlyList<DesktopQuestionPrompt> Questions { get; init; } = [];

    public IReadOnlyList<DesktopQuestionAnswer> Answers { get; init; } = [];
}
