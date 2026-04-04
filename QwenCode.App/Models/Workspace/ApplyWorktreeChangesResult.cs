namespace QwenCode.App.Models;

public sealed class ApplyWorktreeChangesResult
{
    public string SourceRepositoryPath { get; init; } = string.Empty;

    public string WorktreePath { get; init; } = string.Empty;

    public IReadOnlyList<string> AppliedFiles { get; init; } = [];

    public IReadOnlyList<string> DeletedFiles { get; init; } = [];
}
