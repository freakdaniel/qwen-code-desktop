namespace QwenCode.App.Models;

/// <summary>
/// Represents the Workspace Snapshot
/// </summary>
public sealed class WorkspaceSnapshot
{
    /// <summary>
    /// Gets or sets the git
    /// </summary>
    public required GitRepositorySnapshot Git { get; init; }

    /// <summary>
    /// Gets or sets the discovery
    /// </summary>
    public required FileDiscoverySnapshot Discovery { get; init; }
}
