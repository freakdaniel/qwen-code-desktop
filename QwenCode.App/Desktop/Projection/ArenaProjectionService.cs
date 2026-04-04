using QwenCode.App.Agents;
using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public sealed class ArenaProjectionService(IArenaSessionRegistry arenaSessionRegistry) : IDesktopArenaProjectionService
{
    public event EventHandler<ArenaSessionEvent>? ArenaEvent
    {
        add => arenaSessionRegistry.SessionEvent += value;
        remove => arenaSessionRegistry.SessionEvent -= value;
    }

    public Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync() =>
        Task.FromResult(arenaSessionRegistry.ListActiveSessions());

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
