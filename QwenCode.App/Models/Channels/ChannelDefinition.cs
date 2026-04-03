namespace QwenCode.App.Models;

public sealed class ChannelDefinition
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Scope { get; init; }

    public string Description { get; init; } = string.Empty;

    public string SenderPolicy { get; init; } = string.Empty;

    public string SessionScope { get; init; } = string.Empty;

    public string WorkingDirectory { get; init; } = string.Empty;

    public string ApprovalMode { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool SupportsPairing { get; init; }

    public int SessionCount { get; init; }

    public int PendingPairingCount { get; init; }

    public int AllowlistCount { get; init; }
}
