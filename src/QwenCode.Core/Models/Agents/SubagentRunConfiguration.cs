namespace QwenCode.App.Models;

/// <summary>
/// Represents the Subagent Run Configuration
/// </summary>
public sealed class SubagentRunConfiguration
{
    /// <summary>
    /// Gets or sets the max time minutes
    /// </summary>
    public int? MaxTimeMinutes { get; init; }

    /// <summary>
    /// Gets or sets the max turns
    /// </summary>
    public int? MaxTurns { get; init; }
}
