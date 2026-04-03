namespace QwenCode.App.Models;

public sealed class GitWorktreeEntry
{
    public required string Path { get; init; }

    public required string Branch { get; init; }

    public required string Head { get; init; }

    public required string Name { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public required bool IsCurrent { get; init; }

    public required bool IsManaged { get; init; }
}
