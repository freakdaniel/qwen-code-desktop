namespace QwenCode.App.Models;

/// <summary>
/// Represents the Approve Channel Pairing Request
/// </summary>
public sealed class ApproveChannelPairingRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the code
    /// </summary>
    public required string Code { get; init; }
}
