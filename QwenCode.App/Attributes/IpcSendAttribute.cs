namespace QwenCode.App.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IpcSendAttribute(string channel) : Attribute
{
    public string Channel { get; } = channel;
}
