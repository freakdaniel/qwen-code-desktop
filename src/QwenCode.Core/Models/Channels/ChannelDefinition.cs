namespace QwenCode.App.Models;

/// <summary>
/// Represents the Channel Definition
/// </summary>
public sealed class ChannelDefinition
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the type
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender policy
    /// </summary>
    public string SenderPolicy { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the session scope
    /// </summary>
    public string SessionScope { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval mode
    /// </summary>
    public string ApprovalMode { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the status
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the supports pairing
    /// </summary>
    public bool SupportsPairing { get; init; }

    /// <summary>
    /// Gets or sets the session count
    /// </summary>
    public int SessionCount { get; init; }

    /// <summary>
    /// Gets or sets the pending pairing count
    /// </summary>
    public int PendingPairingCount { get; init; }

    /// <summary>
    /// Gets or sets the allowlist count
    /// </summary>
    public int AllowlistCount { get; init; }
}
