namespace QwenCode.App.Models;

public sealed class ChannelDispatchResult
{
    public required string ChannelName { get; init; }

    public required string Status { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public string PairingCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string TranscriptPath { get; init; } = string.Empty;

    public bool CreatedNewSession { get; init; }

    public string AssistantSummary { get; init; } = string.Empty;
}
