namespace QwenCode.App.Models;

public sealed class QwenSurfaceDirectory
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required bool Exists { get; init; }

    public required int ItemCount { get; init; }

    public required string Summary { get; init; }
}
