using System.Text.Json;
using System.Text.Json.Serialization;

namespace QwenCode.App.Models;

public sealed class ChannelRuntimeConfiguration
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public string SenderPolicy { get; init; } = "allowlist";

    public string SessionScope { get; init; } = "user";

    public string WorkingDirectory { get; init; } = string.Empty;

    public string ApprovalMode { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string Instructions { get; init; } = string.Empty;

    public string GroupPolicy { get; init; } = "disabled";

    public string DispatchMode { get; init; } = "collect";

    public string BlockStreaming { get; init; } = "off";

    public ChannelBlockStreamingChunkConfiguration BlockStreamingChunk { get; init; } = new();

    public ChannelBlockStreamingCoalesceConfiguration BlockStreamingCoalesce { get; init; } = new();

    public bool RequireMentionByDefault { get; init; } = true;

    public IReadOnlyList<ChannelGroupRuntimeConfiguration> Groups { get; init; } = [];

    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalSettings { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}
