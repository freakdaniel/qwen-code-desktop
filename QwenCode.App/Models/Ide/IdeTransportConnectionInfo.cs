namespace QwenCode.App.Models;

public sealed class IdeTransportConnectionInfo
{
    public string WorkspacePath { get; init; } = string.Empty;

    public string Port { get; init; } = string.Empty;

    public string AuthToken { get; init; } = string.Empty;

    public string StdioCommand { get; init; } = string.Empty;

    public IReadOnlyList<string> StdioArguments { get; init; } = [];
}
