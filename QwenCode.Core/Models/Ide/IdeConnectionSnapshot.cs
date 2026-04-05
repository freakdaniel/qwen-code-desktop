namespace QwenCode.App.Models;

public sealed class IdeConnectionSnapshot
{
    public string Status { get; init; } = "disconnected";

    public string Details { get; init; } = string.Empty;

    public IdeInfo? Ide { get; init; }

    public string WorkspacePath { get; init; } = string.Empty;

    public string Port { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string AuthToken { get; init; } = string.Empty;

    public bool SupportsDiff { get; init; }

    public IReadOnlyList<string> AvailableTools { get; init; } = [];

    public IdeContextSnapshot? Context { get; init; }
}
