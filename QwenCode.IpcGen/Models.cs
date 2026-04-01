namespace QwenCode.IpcGen;

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
    Type OutputType);
