namespace QwenCode.App.Ipc.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IpcInvokeAttribute(string channel) : Attribute
{
    public string Channel { get; } = channel;
}
