namespace QwenCode.App.Models;

public sealed class RestoreGitCheckpointRequest
{
    public string CommitHash { get; init; } = string.Empty;
}
