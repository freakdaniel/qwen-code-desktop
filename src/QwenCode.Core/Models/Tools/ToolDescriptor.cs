namespace QwenCode.App.Models;

/// <summary>
/// Represents the Tool Descriptor
/// </summary>
public sealed class ToolDescriptor
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
    /// Gets or sets the source path
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Gets or sets the approval state
    /// </summary>
    public required string ApprovalState { get; init; }

    /// <summary>
    /// Gets or sets the approval reason
    /// </summary>
    public required string ApprovalReason { get; init; }
}
