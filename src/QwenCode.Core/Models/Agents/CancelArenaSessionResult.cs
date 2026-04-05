namespace QwenCode.App.Models;

/// <summary>
/// Represents the Cancel Arena Session Result
/// </summary>
public sealed class CancelArenaSessionResult
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the was cancelled
    /// </summary>
    public bool WasCancelled { get; init; }

    /// <summary>
    /// Gets or sets the message
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
