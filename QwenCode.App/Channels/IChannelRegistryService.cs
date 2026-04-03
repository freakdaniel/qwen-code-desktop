using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelRegistryService
{
    ChannelSnapshot Inspect(WorkspacePaths workspace);

    ChannelPairingSnapshot GetPairings(WorkspacePaths workspace, GetChannelPairingRequest request);

    ChannelPairingSnapshot ApprovePairing(WorkspacePaths workspace, ApproveChannelPairingRequest request);
}
