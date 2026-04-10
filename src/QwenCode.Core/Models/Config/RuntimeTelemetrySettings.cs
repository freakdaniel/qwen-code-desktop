namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Runtime Telemetry Settings
/// </summary>
public sealed class RuntimeTelemetrySettings
{
    /// <summary>
    /// Gets or sets the enabled
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets or sets the target
    /// </summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the otlp endpoint
    /// </summary>
    public string OtlpEndpoint { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the otlp protocol
    /// </summary>
    public string OtlpProtocol { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the log prompts
    /// </summary>
    public bool LogPrompts { get; init; }

    /// <summary>
    /// Gets or sets the outfile
    /// </summary>
    public string Outfile { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the use collector
    /// </summary>
    public bool UseCollector { get; init; }
}
