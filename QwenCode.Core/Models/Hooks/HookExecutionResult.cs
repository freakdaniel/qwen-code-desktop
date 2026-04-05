namespace QwenCode.App.Models;

public sealed class HookExecutionResult
{
    public required CommandHookConfiguration Hook { get; init; }

    public bool Success { get; init; }

    public int ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public HookOutput? Output { get; init; }

    public TimeSpan Duration { get; init; }
}
