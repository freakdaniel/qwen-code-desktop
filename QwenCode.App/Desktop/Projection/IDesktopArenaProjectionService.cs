using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopArenaProjectionService
{
    event EventHandler<ArenaSessionEvent>? ArenaEvent;

    Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync();

    Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request);
}
