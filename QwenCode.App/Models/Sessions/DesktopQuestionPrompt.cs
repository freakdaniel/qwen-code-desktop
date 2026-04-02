namespace QwenCode.App.Models;

public sealed class DesktopQuestionPrompt
{
    public string Header { get; init; } = string.Empty;

    public string Question { get; init; } = string.Empty;

    public bool MultiSelect { get; init; }

    public IReadOnlyList<DesktopQuestionOption> Options { get; init; } = [];
}
