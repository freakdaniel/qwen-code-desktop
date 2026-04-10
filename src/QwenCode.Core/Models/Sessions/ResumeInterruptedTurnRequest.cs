namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Resume Interrupted Turn Request
/// </summary>
public sealed class ResumeInterruptedTurnRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the recovery note
    /// </summary>
    public string RecoveryNote { get; init; } = string.Empty;
}
