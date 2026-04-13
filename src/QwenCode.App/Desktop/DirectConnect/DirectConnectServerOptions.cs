namespace QwenCode.App.Desktop.DirectConnect;

/// <summary>
/// Provides configuration for the local direct-connect HTTP server.
/// </summary>
public sealed class DirectConnectServerOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "DirectConnectServer";

    /// <summary>
    /// Gets or sets whether the local server should start with the desktop host.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets or sets the loopback host to bind.
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the port to bind. Zero lets Kestrel choose an available port.
    /// </summary>
    public int Port { get; init; }
}
