namespace QwenCode.App.Models;

/// <summary>
/// Represents the Get Desktop Session Request
/// </summary>
public sealed class GetDesktopSessionRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the offset
    /// </summary>
    public int? Offset { get; init; }

    /// <summary>
    /// Gets or sets the limit
    /// </summary>
    public int? Limit { get; init; }
}
