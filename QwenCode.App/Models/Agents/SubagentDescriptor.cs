namespace QwenCode.App.Models;

public sealed class SubagentDescriptor
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string SystemPrompt { get; init; } = string.Empty;

    public bool IsBuiltin { get; init; }

    public IReadOnlyList<string> Tools { get; init; } = [];
}
