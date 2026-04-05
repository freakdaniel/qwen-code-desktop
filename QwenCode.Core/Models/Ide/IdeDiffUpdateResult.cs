namespace QwenCode.App.Models;

public sealed class IdeDiffUpdateResult
{
    public required string Status { get; init; }

    public string Content { get; init; } = string.Empty;
}
