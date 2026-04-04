namespace QwenCode.App.Models;

public sealed class ChannelAttachment
{
    public string Type { get; init; } = string.Empty;

    public string Data { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string MimeType { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;
}
