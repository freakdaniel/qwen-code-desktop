using QwenCode.App.Runtime;

namespace QwenCode.App.Models;

public sealed class SessionConversationRecord
{
    public required string SessionId { get; init; }

    public required string TranscriptPath { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string GitBranch { get; init; }

    public required string StartTime { get; init; }

    public required string LastUpdated { get; init; }

    public required IReadOnlyList<AssistantConversationMessage> ModelHistory { get; init; }
}
