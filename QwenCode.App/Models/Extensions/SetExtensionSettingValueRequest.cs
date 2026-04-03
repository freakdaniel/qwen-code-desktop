namespace QwenCode.App.Models;

public sealed class SetExtensionSettingValueRequest
{
    public required string Name { get; init; }

    public required string Setting { get; init; }

    public required string Scope { get; init; }

    public required string Value { get; init; }
}
