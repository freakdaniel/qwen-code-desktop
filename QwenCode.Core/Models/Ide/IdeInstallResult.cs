namespace QwenCode.App.Models;

public sealed class IdeInstallResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string CommandPath { get; init; } = string.Empty;
}
