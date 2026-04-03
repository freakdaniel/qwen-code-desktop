namespace QwenCode.App.Infrastructure;

public sealed class GitCommandResult
{
    public required bool Success { get; init; }

    public required int ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;
}
