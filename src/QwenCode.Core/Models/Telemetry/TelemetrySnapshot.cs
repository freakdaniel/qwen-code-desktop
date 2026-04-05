namespace QwenCode.App.Models;

/// <summary>
/// Represents the Telemetry Snapshot
/// </summary>
public sealed class TelemetrySnapshot
{
    /// <summary>
    /// Gets or sets the events path
    /// </summary>
    public required string EventsPath { get; init; }

    /// <summary>
    /// Gets or sets the metrics path
    /// </summary>
    public required string MetricsPath { get; init; }

    /// <summary>
    /// Gets or sets the event count
    /// </summary>
    public int EventCount { get; init; }

    /// <summary>
    /// Gets or sets the metrics
    /// </summary>
    public IReadOnlyDictionary<string, TelemetryMetricAggregate> Metrics { get; init; } = new Dictionary<string, TelemetryMetricAggregate>(StringComparer.OrdinalIgnoreCase);
}
