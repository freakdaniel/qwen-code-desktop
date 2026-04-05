using System.Text.Json.Nodes;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Resolved Provider Configuration
/// </summary>
public sealed class ResolvedProviderConfiguration
{
    /// <summary>
    /// Gets or sets the auth type
    /// </summary>
    public required string AuthType { get; init; }

    /// <summary>
    /// Gets or sets the provider flavor
    /// </summary>
    public required string ProviderFlavor { get; init; }

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets or sets the endpoint
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Gets or sets the api key
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets or sets the api key environment variable
    /// </summary>
    public required string ApiKeyEnvironmentVariable { get; init; }

    /// <summary>
    /// Gets or sets the headers
    /// </summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    /// <summary>
    /// Gets or sets the extra body
    /// </summary>
    public required JsonObject ExtraBody { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is dash scope
    /// </summary>
    public required bool IsDashScope { get; init; }
}
