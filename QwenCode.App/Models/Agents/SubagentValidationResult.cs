namespace QwenCode.App.Models;

public sealed class SubagentValidationResult
{
    public required bool IsValid { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}
