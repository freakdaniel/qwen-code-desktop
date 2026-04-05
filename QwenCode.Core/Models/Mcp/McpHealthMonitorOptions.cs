namespace QwenCode.App.Models;

public sealed class McpHealthMonitorOptions
{
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxConsecutiveFailures { get; init; } = 3;

    public bool AutoReconnect { get; init; } = true;

    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);
}
