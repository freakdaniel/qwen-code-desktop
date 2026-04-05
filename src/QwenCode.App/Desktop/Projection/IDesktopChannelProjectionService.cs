using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Channel Projection Service
/// </summary>
public interface IDesktopChannelProjectionService
{
    /// <summary>
    /// Creates snapshot
    /// </summary>
    /// <returns>The resulting channel snapshot</returns>
    ChannelSnapshot CreateSnapshot();

    /// <summary>
    /// Gets pairings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    Task<ChannelPairingSnapshot> GetPairingsAsync(GetChannelPairingRequest request);

    /// <summary>
    /// Approves pairing async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    Task<ChannelPairingSnapshot> ApprovePairingAsync(ApproveChannelPairingRequest request);
}
