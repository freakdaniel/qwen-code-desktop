namespace QwenCode.App.Models;

public sealed class ArenaSessionConfigFile
{
    public string ArenaSessionId { get; init; } = string.Empty;

    public string SourceRepoPath { get; init; } = string.Empty;

    public string Task { get; init; } = string.Empty;

    public int RoundCount { get; init; }

    public string SelectedWinner { get; init; } = string.Empty;

    public string AppliedWinner { get; init; } = string.Empty;

    public IReadOnlyList<ArenaModelDescriptor> Models { get; init; } = [];

    public IReadOnlyList<string> WorktreeNames { get; init; } = [];

    public string BaseBranch { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public IReadOnlyDictionary<string, ArenaAgentStatusFile> Agents { get; init; } =
        new Dictionary<string, ArenaAgentStatusFile>(StringComparer.OrdinalIgnoreCase);
}
