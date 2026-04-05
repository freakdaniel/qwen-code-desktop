namespace QwenCode.App.Models;

/// <summary>
/// Represents the Disconnect Auth Request
/// </summary>
public sealed class DisconnectAuthRequest
{
    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the clear persisted credentials
    /// </summary>
    public bool ClearPersistedCredentials { get; init; } = true;
}
