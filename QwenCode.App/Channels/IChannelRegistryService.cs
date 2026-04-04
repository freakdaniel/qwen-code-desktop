using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelRegistryService
{
    ChannelSnapshot Inspect(WorkspacePaths workspace);

    ChannelDefinition GetChannel(WorkspacePaths workspace, string name);

    ChannelRuntimeConfiguration GetRuntimeConfiguration(WorkspacePaths workspace, string name);

    ChannelSenderAccessDecision EvaluateSenderAccess(WorkspacePaths workspace, string channelName, string senderId, string senderName);

    ChannelPairingSnapshot GetPairings(WorkspacePaths workspace, GetChannelPairingRequest request);

    ChannelPairingSnapshot ApprovePairing(WorkspacePaths workspace, ApproveChannelPairingRequest request);
}
