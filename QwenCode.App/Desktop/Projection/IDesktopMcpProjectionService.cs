using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopMcpProjectionService
{
    McpSnapshot CreateSnapshot();

    Task<McpSnapshot> AddServerAsync(McpServerRegistrationRequest request);

    Task<McpSnapshot> RemoveServerAsync(RemoveMcpServerRequest request);

    Task<McpSnapshot> ReconnectServerAsync(ReconnectMcpServerRequest request, CancellationToken cancellationToken = default);
}
