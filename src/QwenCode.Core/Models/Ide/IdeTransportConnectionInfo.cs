namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Ide Transport Connection Info
/// </summary>
public sealed class IdeTransportConnectionInfo
{
    /// <summary>
    /// Gets or sets the workspace path
    /// </summary>
    public string WorkspacePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the port
    /// </summary>
    public string Port { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth token
    /// </summary>
    public string AuthToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stdio command
    /// </summary>
    public string StdioCommand { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the stdio arguments
    /// </summary>
    public IReadOnlyList<string> StdioArguments { get; init; } = [];
}
