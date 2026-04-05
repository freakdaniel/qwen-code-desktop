namespace QwenCode.App.Ipc.Attributes;

/// <summary>
/// Represents the Ipc Invoke Attribute
/// </summary>
/// <param name="channel">The channel</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IpcInvokeAttribute(string channel) : Attribute
{
    /// <summary>
    /// Gets the channel
    /// </summary>
    public string Channel { get; } = channel;
}
