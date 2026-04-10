namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Native Tool Registration
/// </summary>
public sealed class NativeToolRegistration
{
    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the kind
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is implemented
    /// </summary>
    public bool IsImplemented { get; init; }

    /// <summary>
    /// Gets or sets the approval state
    /// </summary>
    public required string ApprovalState { get; init; }

    /// <summary>
    /// Gets or sets the approval reason
    /// </summary>
    public required string ApprovalReason { get; init; }
}
