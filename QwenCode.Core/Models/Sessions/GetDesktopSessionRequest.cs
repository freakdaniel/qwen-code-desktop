namespace QwenCode.App.Models;

public sealed class GetDesktopSessionRequest
{
    public string SessionId { get; init; } = string.Empty;

    public int? Offset { get; init; }

    public int? Limit { get; init; }
}
