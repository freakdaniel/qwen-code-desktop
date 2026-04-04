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

    public string Instructions { get; init; } = string.Empty;

    public string GroupPolicy { get; init; } = "disabled";

    public string DispatchMode { get; init; } = "collect";

    public bool RequireMentionByDefault { get; init; } = true;

    public IReadOnlyList<ChannelGroupRuntimeConfiguration> Groups { get; init; } = [];
}
