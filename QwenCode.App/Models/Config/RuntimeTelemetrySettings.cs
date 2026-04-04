namespace QwenCode.App.Models;

public sealed class RuntimeTelemetrySettings
{
    public bool Enabled { get; init; }

    public string Target { get; init; } = string.Empty;

    public string OtlpEndpoint { get; init; } = string.Empty;

    public string OtlpProtocol { get; init; } = string.Empty;

    public bool LogPrompts { get; init; }

    public string Outfile { get; init; } = string.Empty;

    public bool UseCollector { get; init; }
}
