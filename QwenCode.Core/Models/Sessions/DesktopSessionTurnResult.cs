namespace QwenCode.App.Models;

public sealed class DesktopSessionTurnResult
{
    public required SessionPreview Session { get; init; }

    public required string AssistantSummary { get; init; }

    public required bool CreatedNewSession { get; init; }

    public required NativeToolExecutionResult ToolExecution { get; init; }

    public ResolvedCommand? ResolvedCommand { get; init; }
}
