namespace QwenCode.App.Models;

public sealed class ActiveTurnState
{
    public required string SessionId { get; init; }

    public required string Prompt { get; set; }

    public required string TranscriptPath { get; set; }

    public required string WorkingDirectory { get; set; }

    public required string GitBranch { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public string Stage { get; set; } = "turn-started";

    public string Status { get; set; } = "started";

    public string ContentSnapshot { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }
}
