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

    public string Model { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;

    public SubagentRunConfiguration RunConfiguration { get; init; } = new();

    public IReadOnlyList<string> ValidationWarnings { get; init; } = [];
}
