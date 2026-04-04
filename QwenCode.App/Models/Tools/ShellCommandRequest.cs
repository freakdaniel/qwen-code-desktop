namespace QwenCode.App.Models;

public sealed class ShellCommandRequest
{
    public required string Command { get; init; }

    public required string WorkingDirectory { get; init; }

    public TimeSpan? Timeout { get; init; }
}
