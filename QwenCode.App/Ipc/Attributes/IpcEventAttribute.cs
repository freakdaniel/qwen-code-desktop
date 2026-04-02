namespace QwenCode.App.Ipc.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IpcEventAttribute(string channel) : Attribute
{
    public string Channel { get; } = channel;
}
