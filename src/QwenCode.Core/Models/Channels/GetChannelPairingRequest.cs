namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Get Channel Pairing Request
/// </summary>
public sealed class GetChannelPairingRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }
}
