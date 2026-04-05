namespace QwenCode.App.Models;

/// <summary>
/// Represents the Channel Attachment
/// </summary>
public sealed class ChannelAttachment
{
    /// <summary>
    /// Gets or sets the type
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the data
    /// </summary>
    public string Data { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the file path
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the mime type
    /// </summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name
    /// </summary>
    public string FileName { get; init; } = string.Empty;
}
