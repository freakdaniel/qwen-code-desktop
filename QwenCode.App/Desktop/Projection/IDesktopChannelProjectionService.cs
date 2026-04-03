using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopChannelProjectionService
{
    ChannelSnapshot CreateSnapshot();

    Task<ChannelPairingSnapshot> GetPairingsAsync(GetChannelPairingRequest request);

    Task<ChannelPairingSnapshot> ApprovePairingAsync(ApproveChannelPairingRequest request);
}
