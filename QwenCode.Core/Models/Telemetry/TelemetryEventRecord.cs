using System.Text.Json.Nodes;

namespace QwenCode.App.Models;

public sealed class TelemetryEventRecord
{
    public required string EventName { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public string SessionId { get; init; } = string.Empty;

    public JsonObject Payload { get; init; } = [];
}
