namespace QwenCode.App.Models;

public sealed class PromptRegistrySnapshot
{
    public int TotalCount { get; init; }

    public int ServerCount { get; init; }

    public IReadOnlyList<PromptRegistryEntry> Prompts { get; init; } = [];
}
