using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Defines the contract for Channel Registry Service
/// </summary>
public interface IChannelRegistryService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <returns>The resulting channel snapshot</returns>
    ChannelSnapshot Inspect(WorkspacePaths workspace);

    /// <summary>
    /// Gets channel
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="name">The name</param>
    /// <returns>The resulting channel definition</returns>
    ChannelDefinition GetChannel(WorkspacePaths workspace, string name);

    /// <summary>
    /// Gets runtime configuration
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="name">The name</param>
    /// <returns>The resulting channel runtime configuration</returns>
    ChannelRuntimeConfiguration GetRuntimeConfiguration(WorkspacePaths workspace, string name);

    /// <summary>
    /// Executes evaluate sender access
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelName">The channel name</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="senderName">The sender name</param>
    /// <returns>The resulting channel sender access decision</returns>
    ChannelSenderAccessDecision EvaluateSenderAccess(WorkspacePaths workspace, string channelName, string senderId, string senderName);

    /// <summary>
    /// Gets pairings
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting channel pairing snapshot</returns>
    ChannelPairingSnapshot GetPairings(WorkspacePaths workspace, GetChannelPairingRequest request);

    /// <summary>
    /// Approves pairing
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting channel pairing snapshot</returns>
    ChannelPairingSnapshot ApprovePairing(WorkspacePaths workspace, ApproveChannelPairingRequest request);
}
