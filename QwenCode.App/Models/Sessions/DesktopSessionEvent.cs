using QwenCode.App.Models;

namespace QwenCode.App.Models;

public sealed class DesktopSessionEvent
{
    public required string SessionId { get; init; }

    public required DesktopSessionEventKind Kind { get; init; }

    public required DateTime TimestampUtc { get; init; }

    public required string Message { get; init; }

    public string WorkingDirectory { get; init; } = string.Empty;

    public string GitBranch { get; init; } = string.Empty;

    public string CommandName { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string ContentDelta { get; init; } = string.Empty;

    public string ContentSnapshot { get; init; } = string.Empty;
}
