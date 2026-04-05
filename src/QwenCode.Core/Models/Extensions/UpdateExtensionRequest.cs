namespace QwenCode.App.Models;

/// <summary>
/// Represents the Update Extension Request
/// </summary>
public sealed class UpdateExtensionRequest
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the update all
    /// </summary>
    public bool UpdateAll { get; init; }
}
