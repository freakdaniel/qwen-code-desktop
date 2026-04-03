namespace QwenCode.App.Models;

public sealed class McpResourceReadResult
{
    public required string ServerName { get; init; }

    public required string Uri { get; init; }

    public string Output { get; init; } = string.Empty;
}
