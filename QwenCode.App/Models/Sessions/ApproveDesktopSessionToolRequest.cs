namespace QwenCode.App.Models;

public sealed class ApproveDesktopSessionToolRequest
{
    public string SessionId { get; init; } = string.Empty;

    public string EntryId { get; init; } = string.Empty;
}
