namespace QwenCode.App.Models;

/// <summary>
/// Represents the Native Tool Host Snapshot
/// </summary>
public sealed class NativeToolHostSnapshot
{
    /// <summary>
    /// Gets or sets the registered count
    /// </summary>
    public int RegisteredCount { get; init; }

    /// <summary>
    /// Gets or sets the implemented count
    /// </summary>
    public int ImplementedCount { get; init; }

    /// <summary>
    /// Gets or sets the ready count
    /// </summary>
    public int ReadyCount { get; init; }

    /// <summary>
    /// Gets or sets the approval required count
    /// </summary>
    public int ApprovalRequiredCount { get; init; }

    /// <summary>
    /// Gets or sets the tools
    /// </summary>
    public required IReadOnlyList<NativeToolRegistration> Tools { get; init; }
}
