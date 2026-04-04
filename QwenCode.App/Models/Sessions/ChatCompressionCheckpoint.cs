namespace QwenCode.App.Models;

public sealed class ChatCompressionCheckpoint
{
    public required string Summary { get; init; }

    public int CompressedEntryCount { get; init; }

    public int PreservedEntryCount { get; init; }

    public int EstimatedTokenCount { get; init; }

    public int EstimatedContextWindowTokens { get; init; }

    public double EstimatedContextPercentage { get; init; }

    public double ThresholdPercentage { get; init; }

    public string Trigger { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
}
