namespace QwenCode.App.Models;

public sealed class StartDesktopSessionTurnRequest
{
    public string SessionId { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string ToolArgumentsJson { get; init; } = "{}";

    public bool ApproveToolExecution { get; init; }
}
