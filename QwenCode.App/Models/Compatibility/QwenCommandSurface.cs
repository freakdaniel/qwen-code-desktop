namespace QwenCode.App.Models;

public sealed class QwenCommandSurface
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Scope { get; init; }

    public required string Path { get; init; }

    public required string Description { get; init; }

    public required string Group { get; init; }
}
