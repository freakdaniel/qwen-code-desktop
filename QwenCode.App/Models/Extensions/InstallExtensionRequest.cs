namespace QwenCode.App.Models;

public sealed class InstallExtensionRequest
{
    public required string SourcePath { get; init; }

    public required string InstallMode { get; init; }

    public string SourceType { get; init; } = "local";

    public string Ref { get; init; } = string.Empty;

    public bool AutoUpdate { get; init; }

    public bool AllowPreRelease { get; init; }

    public string RegistryUrl { get; init; } = string.Empty;
}
