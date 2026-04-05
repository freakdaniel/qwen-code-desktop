namespace QwenCode.App.Models;

public sealed class SessionRecordingContext
{
    public required string SessionId { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string GitBranch { get; init; }

    public required string TitleHint { get; init; }

    public required string Status { get; init; }
}
