namespace QwenCode.App.Models;

public sealed class TelemetrySnapshot
{
    public required string EventsPath { get; init; }

    public required string MetricsPath { get; init; }

    public int EventCount { get; init; }

    public IReadOnlyDictionary<string, TelemetryMetricAggregate> Metrics { get; init; } = new Dictionary<string, TelemetryMetricAggregate>(StringComparer.OrdinalIgnoreCase);
}
