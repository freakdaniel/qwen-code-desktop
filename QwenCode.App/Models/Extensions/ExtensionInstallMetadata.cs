namespace QwenCode.App.Models;

public sealed class ExtensionInstallMetadata
{
    public required string Source { get; init; }

    public required string Type { get; init; }

    public string? Ref { get; init; }

    public bool? AutoUpdate { get; init; }

    public bool? AllowPreRelease { get; init; }

    public string? RegistryUrl { get; init; }

    public string? ReleaseTag { get; init; }
}
