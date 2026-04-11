using QwenCode.Core.Models;

namespace QwenCode.App.Desktop.DirectConnect;

/// <summary>
/// Defines the contract for the local direct-connect HTTP server.
/// </summary>
public interface IDirectConnectServerHost
{
    /// <summary>
    /// Gets the current server state.
    /// </summary>
    DirectConnectServerState State { get; }

    /// <summary>
    /// Starts the local server if enabled.
    /// </summary>
    Task<DirectConnectServerState> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the local server if it is running.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
