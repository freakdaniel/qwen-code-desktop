namespace QwenCode.App.Models;

public sealed class ExtensionSettingDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string EnvironmentVariable { get; init; }

    public required bool Sensitive { get; init; }
}
