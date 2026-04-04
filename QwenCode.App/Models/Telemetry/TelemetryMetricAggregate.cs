namespace QwenCode.App.Models;

public sealed class TelemetryMetricAggregate
{
    public long Count { get; init; }

    public double Sum { get; init; }

    public double Min { get; init; }

    public double Max { get; init; }

    public string Unit { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> LastTags { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public DateTime LastUpdatedUtc { get; init; } = DateTime.UtcNow;
}
