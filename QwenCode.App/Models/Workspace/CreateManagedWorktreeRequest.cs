namespace QwenCode.App.Models;

public sealed class CreateManagedWorktreeRequest
{
    public required string SessionId { get; init; }

    public required string Name { get; init; }

    public string BaseBranch { get; init; } = string.Empty;
}
