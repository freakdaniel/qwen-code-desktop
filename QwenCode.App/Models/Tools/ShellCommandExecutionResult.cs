namespace QwenCode.App.Models;

public sealed class ShellCommandExecutionResult
{
    public required string WorkingDirectory { get; init; }

    public required string Output { get; init; }

    public required string ErrorMessage { get; init; }

    public required int ExitCode { get; init; }

    public required bool TimedOut { get; init; }

    public required bool Cancelled { get; init; }
}
