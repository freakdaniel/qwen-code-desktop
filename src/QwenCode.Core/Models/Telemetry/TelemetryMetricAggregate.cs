namespace QwenCode.App.Models;

/// <summary>
/// Represents the Telemetry Metric Aggregate
/// </summary>
public sealed class TelemetryMetricAggregate
{
    /// <summary>
    /// Gets or sets the count
    /// </summary>
    public long Count { get; init; }

    /// <summary>
    /// Gets or sets the sum
    /// </summary>
    public double Sum { get; init; }

    /// <summary>
    /// Gets or sets the min
    /// </summary>
    public double Min { get; init; }

    /// <summary>
    /// Gets or sets the max
    /// </summary>
    public double Max { get; init; }

    /// <summary>
    /// Gets or sets the unit
    /// </summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the last tags
    /// </summary>
    public IReadOnlyDictionary<string, string> LastTags { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the last updated utc
    /// </summary>
    public DateTime LastUpdatedUtc { get; init; } = DateTime.UtcNow;
}
