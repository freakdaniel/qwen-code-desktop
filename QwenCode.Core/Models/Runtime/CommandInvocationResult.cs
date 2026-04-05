using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class CommandInvocationResult
{
    public required ResolvedCommand Command { get; init; }

    public required string Status { get; init; }

    public string Output { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public bool IsTerminal { get; init; }
}
