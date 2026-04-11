namespace QwenCode.Core.Models;

/// <summary>
/// Represents one session event in a direct-connect event stream.
/// </summary>
public sealed class DirectConnectSessionEventRecord
{
    /// <summary>
    /// Gets or sets the monotonically increasing event sequence.
    /// </summary>
    public required long Sequence { get; init; }

    /// <summary>
    /// Gets or sets the underlying desktop session event.
    /// </summary>
    public required DesktopSessionEvent Event { get; init; }
}
