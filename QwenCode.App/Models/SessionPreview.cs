using QwenCode.App.Enums;

namespace QwenCode.App.Models;

public sealed class SessionPreview
{
    public required string SessionId { get; init; }

    public required string Title { get; init; }

    public required string LastActivity { get; init; }

    public required string Category { get; init; }

    public required DesktopMode Mode { get; init; }

    public required string Status { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string GitBranch { get; init; }

    public int MessageCount { get; init; }

    public required string TranscriptPath { get; init; }
}
