namespace QwenCode.App.Models;

/// <summary>
/// Represents the Start Qwen O Auth Device Flow Request
/// </summary>
public sealed class StartQwenOAuthDeviceFlowRequest
{
    /// <summary>
    /// Gets or sets the scope
    /// </summary>
    public required string Scope { get; init; }
}
