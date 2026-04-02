namespace QwenCode.App.Models;

public sealed class McpReconnectResult
{
    public required string Name { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset AttemptedAtUtc { get; init; }

    public string Message { get; init; } = string.Empty;
}
