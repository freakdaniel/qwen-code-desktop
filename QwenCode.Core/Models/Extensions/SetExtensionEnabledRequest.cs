namespace QwenCode.App.Models;

public sealed class SetExtensionEnabledRequest
{
    public required string Name { get; init; }

    public required string Scope { get; init; }

    public required bool Enabled { get; init; }
}
