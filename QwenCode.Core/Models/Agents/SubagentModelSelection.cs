namespace QwenCode.App.Models;

public sealed class SubagentModelSelection
{
    public string AuthType { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public bool Inherits { get; init; }
}
