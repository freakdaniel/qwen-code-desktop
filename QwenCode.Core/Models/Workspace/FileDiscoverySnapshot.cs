namespace QwenCode.App.Models;

public sealed class FileDiscoverySnapshot
{
    public required bool GitAware { get; init; }

    public required bool HasQwenIgnore { get; init; }

    public required int CandidateFileCount { get; init; }

    public required int VisibleFileCount { get; init; }

    public required int GitIgnoredCount { get; init; }

    public required int QwenIgnoredCount { get; init; }

    public required int QwenIgnorePatternCount { get; init; }

    public required IReadOnlyList<string> ContextFiles { get; init; }

    public required IReadOnlyList<string> SampleVisibleFiles { get; init; }

    public required IReadOnlyList<string> SampleGitIgnoredFiles { get; init; }

    public required IReadOnlyList<string> SampleQwenIgnoredFiles { get; init; }
}
