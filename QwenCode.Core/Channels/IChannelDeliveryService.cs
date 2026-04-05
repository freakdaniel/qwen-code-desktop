using QwenCode.App.Models;

namespace QwenCode.App.Channels;

public interface IChannelDeliveryService
{
    Task DeliverAsync(DesktopSessionEvent sessionEvent, CancellationToken cancellationToken = default);

    Task<int> ReplayQueuedAsync(WorkspacePaths workspace, CancellationToken cancellationToken = default);
}
