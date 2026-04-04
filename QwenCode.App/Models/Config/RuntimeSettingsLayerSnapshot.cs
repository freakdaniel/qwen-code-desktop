namespace QwenCode.App.Models;

public sealed class RuntimeSettingsLayerSnapshot
{
    public required string Scope { get; init; }

    public required string Source { get; init; }

    public required string Path { get; init; }

    public required bool Included { get; init; }
}
