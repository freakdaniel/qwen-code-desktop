namespace QwenCode.App.Models;

public sealed class ExtensionSettingValue
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string EnvironmentVariable { get; init; }

    public required bool Sensitive { get; init; }

    public string UserValue { get; init; } = string.Empty;

    public string WorkspaceValue { get; init; } = string.Empty;

    public string EffectiveValue { get; init; } = string.Empty;

    public bool HasUserValue { get; init; }

    public bool HasWorkspaceValue { get; init; }
}
