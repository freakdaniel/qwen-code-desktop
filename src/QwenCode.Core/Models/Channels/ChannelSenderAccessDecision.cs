namespace QwenCode.App.Models;

/// <summary>
/// Represents the Channel Sender Access Decision
/// </summary>
public sealed class ChannelSenderAccessDecision
{
    /// <summary>
    /// Gets or sets the allowed
    /// </summary>
    public bool Allowed { get; init; }

    /// <summary>
    /// Gets or sets the pairing code
    /// </summary>
    public string PairingCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the reason
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}
