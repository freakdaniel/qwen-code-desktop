namespace QwenCode.App.Models;

public sealed class ExtensionSettingsSnapshot
{
    public required string ExtensionName { get; init; }

    public required string Version { get; init; }

    public required string InstallType { get; init; }

    public required string Path { get; init; }

    public required IReadOnlyList<ExtensionSettingValue> Settings { get; init; }
}
