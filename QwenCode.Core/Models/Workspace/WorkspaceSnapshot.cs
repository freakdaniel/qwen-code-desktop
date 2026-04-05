namespace QwenCode.App.Models;

public sealed class WorkspaceSnapshot
{
    public required GitRepositorySnapshot Git { get; init; }

    public required FileDiscoverySnapshot Discovery { get; init; }
}
