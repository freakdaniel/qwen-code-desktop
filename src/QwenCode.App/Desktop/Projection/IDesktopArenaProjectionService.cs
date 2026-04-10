using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Defines the contract for Desktop Arena Projection Service
/// </summary>
public interface IDesktopArenaProjectionService
{
    /// <summary>
    /// Occurs when Arena Event
    /// </summary>
    event EventHandler<ArenaSessionEvent>? ArenaEvent;

    /// <summary>
    /// Gets active arena sessions async
    /// </summary>
    /// <returns>A task that resolves to i read only list active arena session state</returns>
    Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync();

    /// <summary>
    /// Cancels arena session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel arena session result</returns>
    Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request);
}
