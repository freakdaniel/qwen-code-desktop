namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Cancel Qwen O Auth Device Flow Request
/// </summary>
public sealed class CancelQwenOAuthDeviceFlowRequest
{
    /// <summary>
    /// Gets or sets the flow id
    /// </summary>
    public string FlowId { get; init; } = string.Empty;
}
