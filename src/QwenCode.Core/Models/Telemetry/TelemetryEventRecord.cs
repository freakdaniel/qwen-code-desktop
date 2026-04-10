using System.Text.Json.Nodes;

namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Telemetry Event Record
/// </summary>
public sealed class TelemetryEventRecord
{
    /// <summary>
    /// Gets or sets the event name
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets or sets the timestamp utc
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the payload
    /// </summary>
    public JsonObject Payload { get; init; } = [];
}
