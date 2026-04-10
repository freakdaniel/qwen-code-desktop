namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Cancel Arena Session Request
/// </summary>
public sealed class CancelArenaSessionRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;
}
