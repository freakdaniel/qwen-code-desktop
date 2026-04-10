namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Resolved Token Limits
/// </summary>
public sealed class ResolvedTokenLimits
{
    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Gets or sets the normalized model
    /// </summary>
    public required string NormalizedModel { get; init; }

    /// <summary>
    /// Gets or sets the input token limit
    /// </summary>
    public required int InputTokenLimit { get; init; }

    /// <summary>
    /// Gets or sets the output token limit
    /// </summary>
    public required int OutputTokenLimit { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether has explicit output limit
    /// </summary>
    public required bool HasExplicitOutputLimit { get; init; }
}
