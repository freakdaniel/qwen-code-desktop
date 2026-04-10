namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Configure Coding Plan Auth Request
/// </summary>
public sealed class ConfigureCodingPlanAuthRequest
{
    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Gets or sets the region
    /// </summary>
    public required string Region { get; init; }

    /// <summary>
    /// Gets or sets the api key
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets or sets the model
    /// </summary>
    public string Model { get; init; } = string.Empty;
}
