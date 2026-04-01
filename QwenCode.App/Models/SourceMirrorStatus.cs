namespace QwenCode.App.Models;

public sealed class SourceMirrorStatus
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Path { get; init; }

    public required string Status { get; init; }

    public required string Summary { get; init; }

    public required bool Exists { get; init; }

    public required bool IsGitRepository { get; init; }

    public string? PrimaryMarker { get; init; }

    public required IReadOnlyList<string> Highlights { get; init; }
}
