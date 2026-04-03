namespace QwenCode.App.Models;

public sealed class ChatCompressionCheckpoint
{
    public required string Summary { get; init; }

    public int CompressedEntryCount { get; init; }

    public int PreservedEntryCount { get; init; }
}
