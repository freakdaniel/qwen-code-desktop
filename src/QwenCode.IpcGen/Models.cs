namespace QwenCode.IpcGen;

using System.Reflection;

internal enum IpcKind
{
    Invoke,
    Send,
    Event
}

internal sealed record IpcMethod(
    IpcKind Kind,
    string Channel,
    string MethodName,
    Type? InputType,
    NullabilityInfo? InputNullability,
    Type OutputType,
    NullabilityInfo? OutputNullability);
