using System.Text.Json;
using System.Text.Json.Serialization;

namespace QwenCode.App.Models;

/// <summary>
/// Represents the Channel Runtime Configuration
/// </summary>
public sealed class ChannelRuntimeConfiguration
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the type
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the sender policy
    /// </summary>
    public string SenderPolicy { get; init; } = "allowlist";

    /// <summary>
    /// Gets or sets the session scope
    /// </summary>
    public string SessionScope { get; init; } = "user";

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval mode
    /// </summary>
    public string ApprovalMode { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the token
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the client id
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret
    /// </summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the base url
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the instructions
    /// </summary>
    public string Instructions { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the group policy
    /// </summary>
    public string GroupPolicy { get; init; } = "disabled";

    /// <summary>
    /// Gets or sets the dispatch mode
    /// </summary>
    public string DispatchMode { get; init; } = "collect";

    /// <summary>
    /// Gets or sets the block streaming
    /// </summary>
    public string BlockStreaming { get; init; } = "off";

    /// <summary>
    /// Gets or sets the block streaming chunk
    /// </summary>
    public ChannelBlockStreamingChunkConfiguration BlockStreamingChunk { get; init; } = new();

    /// <summary>
    /// Gets or sets the block streaming coalesce
    /// </summary>
    public ChannelBlockStreamingCoalesceConfiguration BlockStreamingCoalesce { get; init; } = new();

    /// <summary>
    /// Gets or sets the require mention by default
    /// </summary>
    public bool RequireMentionByDefault { get; init; } = true;

    /// <summary>
    /// Gets or sets the groups
    /// </summary>
    public IReadOnlyList<ChannelGroupRuntimeConfiguration> Groups { get; init; } = [];

    /// <summary>
    /// Gets or sets the additional settings
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalSettings { get; init; } =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}
