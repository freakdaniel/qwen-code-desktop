using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Defines the contract for Channel Delivery Service
/// </summary>
public interface IChannelDeliveryService
{
    /// <summary>
    /// Executes deliver async
    /// </summary>
    /// <param name="sessionEvent">The session event</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeliverAsync(DesktopSessionEvent sessionEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays queued async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to int</returns>
    Task<int> ReplayQueuedAsync(WorkspacePaths workspace, CancellationToken cancellationToken = default);
}
