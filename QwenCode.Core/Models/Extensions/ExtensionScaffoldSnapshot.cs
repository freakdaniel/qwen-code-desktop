namespace QwenCode.App.Models;

public sealed class ExtensionScaffoldSnapshot
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string Template { get; init; }

    public required bool CreatedManifest { get; init; }

    public required IReadOnlyList<string> CreatedFiles { get; init; }
}
