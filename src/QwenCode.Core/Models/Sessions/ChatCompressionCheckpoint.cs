namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Chat Compression Checkpoint
/// </summary>
public sealed class ChatCompressionCheckpoint
{
    /// <summary>
    /// Gets or sets the summary
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets or sets the compressed entry count
    /// </summary>
    public int CompressedEntryCount { get; init; }

    /// <summary>
    /// Gets or sets the preserved entry count
    /// </summary>
    public int PreservedEntryCount { get; init; }

    /// <summary>
    /// Gets or sets the estimated token count
    /// </summary>
    public int EstimatedTokenCount { get; init; }

    /// <summary>
    /// Gets or sets the estimated context window tokens
    /// </summary>
    public int EstimatedContextWindowTokens { get; init; }

    /// <summary>
    /// Gets or sets the estimated context percentage
    /// </summary>
    public double EstimatedContextPercentage { get; init; }

    /// <summary>
    /// Gets or sets the threshold percentage
    /// </summary>
    public double ThresholdPercentage { get; init; }

    /// <summary>
    /// Gets or sets the trigger
    /// </summary>
    public string Trigger { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the created at utc
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }
}
