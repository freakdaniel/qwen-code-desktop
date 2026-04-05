namespace QwenCode.App.Models;

public sealed class RemoveMcpServerRequest
{
    public required string Name { get; init; }

    public required string Scope { get; init; }
}
