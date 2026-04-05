namespace QwenCode.App.Models;

public sealed class SubagentToolExecutionRecord
{
    public string ToolName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string ApprovalState { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> ChangedFiles { get; init; } = [];
}
