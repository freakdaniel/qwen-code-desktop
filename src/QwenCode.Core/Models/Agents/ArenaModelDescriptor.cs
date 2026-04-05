namespace QwenCode.App.Models;

/// <summary>
/// Represents the Arena Model Descriptor
/// </summary>
public sealed class ArenaModelDescriptor
{
    /// <summary>
    /// Gets or sets the agent name
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth type
    /// </summary>
    public string AuthType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the api key
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the base url
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;
}
