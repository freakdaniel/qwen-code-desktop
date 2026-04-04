namespace QwenCode.App.Models;

public sealed class CancelArenaSessionResult
{
    public string SessionId { get; init; } = string.Empty;

    public bool WasCancelled { get; init; }

    public string Message { get; init; } = string.Empty;
}
