using System.Text.Json.Nodes;

namespace QwenCode.App.Runtime;

public sealed class ResolvedProviderConfiguration
{
    public required string AuthType { get; init; }

    public required string Model { get; init; }

    public required string Endpoint { get; init; }

    public required string ApiKey { get; init; }

    public required string ApiKeyEnvironmentVariable { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public required JsonObject ExtraBody { get; init; }

    public required bool IsDashScope { get; init; }
}
