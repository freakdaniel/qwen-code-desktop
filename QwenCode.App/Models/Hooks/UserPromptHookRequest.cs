namespace QwenCode.App.Models;

public sealed class UserPromptHookRequest
{
    public required string SessionId { get; init; }

    public required string Prompt { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string TranscriptPath { get; init; }
}
