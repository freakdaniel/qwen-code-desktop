namespace QwenCode.App.Models;

public sealed class ChannelSnapshot
{
    public required bool IsServiceRunning { get; init; }

    public int? ServiceProcessId { get; init; }

    public string ServiceStartedAtUtc { get; init; } = string.Empty;

    public string ServiceUptimeText { get; init; } = string.Empty;

    public required IReadOnlyList<string> SupportedTypes { get; init; }

    public required IReadOnlyList<ChannelDefinition> Channels { get; init; }
}
