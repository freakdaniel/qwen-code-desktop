namespace QwenCode.Core.Models;

/// <summary>
/// Represents the File Discovery Snapshot
/// </summary>
public sealed class FileDiscoverySnapshot
{
    /// <summary>
    /// Gets or sets the git aware
    /// </summary>
    public required bool GitAware { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has qwen ignore
    /// </summary>
    public required bool HasQwenIgnore { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether candidate file count
    /// </summary>
    public required int CandidateFileCount { get; init; }

    /// <summary>
    /// Gets or sets the visible file count
    /// </summary>
    public required int VisibleFileCount { get; init; }

    /// <summary>
    /// Gets or sets the git ignored count
    /// </summary>
    public required int GitIgnoredCount { get; init; }

    /// <summary>
    /// Gets or sets the qwen ignored count
    /// </summary>
    public required int QwenIgnoredCount { get; init; }

    /// <summary>
    /// Gets or sets the qwen ignore pattern count
    /// </summary>
    public required int QwenIgnorePatternCount { get; init; }

    /// <summary>
    /// Gets or sets the context files
    /// </summary>
    public required IReadOnlyList<string> ContextFiles { get; init; }

    /// <summary>
    /// Gets or sets the sample visible files
    /// </summary>
    public required IReadOnlyList<string> SampleVisibleFiles { get; init; }

    /// <summary>
    /// Gets or sets the sample git ignored files
    /// </summary>
    public required IReadOnlyList<string> SampleGitIgnoredFiles { get; init; }

    /// <summary>
    /// Gets or sets the sample qwen ignored files
    /// </summary>
    public required IReadOnlyList<string> SampleQwenIgnoredFiles { get; init; }
}
