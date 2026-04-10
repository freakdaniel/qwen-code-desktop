namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Prompt Registry Snapshot
/// </summary>
public sealed class PromptRegistrySnapshot
{
    /// <summary>
    /// Gets or sets the total count
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets or sets the server count
    /// </summary>
    public int ServerCount { get; init; }

    /// <summary>
    /// Gets or sets the prompts
    /// </summary>
    public IReadOnlyList<PromptRegistryEntry> Prompts { get; init; } = [];
}
