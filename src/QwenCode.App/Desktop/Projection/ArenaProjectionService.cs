using QwenCode.Core.Agents;
using QwenCode.Core.Models;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Arena Projection Service
/// </summary>
/// <param name="arenaSessionRegistry">The arena session registry</param>
public sealed class ArenaProjectionService(IArenaSessionRegistry arenaSessionRegistry) : IDesktopArenaProjectionService
{
    /// <summary>
    /// Occurs when Arena Event
    /// </summary>
    public event EventHandler<ArenaSessionEvent>? ArenaEvent
    {
        add => arenaSessionRegistry.SessionEvent += value;
        remove => arenaSessionRegistry.SessionEvent -= value;
    }

    /// <summary>
    /// Gets active arena sessions async
    /// </summary>
    /// <returns>A task that resolves to i read only list active arena session state</returns>
    public Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync() =>
        Task.FromResult(arenaSessionRegistry.ListActiveSessions());

    /// <summary>
    /// Cancels arena session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel arena session result</returns>
    public Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request)
    {
        var sessionId = request.SessionId.Trim();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Task.FromResult(new CancelArenaSessionResult
            {
                SessionId = string.Empty,
                WasCancelled = false,
                Message = "Arena session id is required."
            });
        }

        var wasCancelled = arenaSessionRegistry.Cancel(sessionId, $"Cancellation requested for arena session '{sessionId}'.");
        return Task.FromResult(new CancelArenaSessionResult
        {
            SessionId = sessionId,
            WasCancelled = wasCancelled,
            Message = wasCancelled
                ? $"Cancellation requested for arena session '{sessionId}'."
                : $"Arena session '{sessionId}' is not active."
        });
    }
}
