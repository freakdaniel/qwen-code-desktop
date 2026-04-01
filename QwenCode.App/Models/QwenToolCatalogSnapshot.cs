namespace QwenCode.App.Models;

public sealed class QwenToolCatalogSnapshot
{
    public required string SourceMode { get; init; }

    public int TotalCount { get; init; }

    public int AllowedCount { get; init; }

    public int AskCount { get; init; }

    public int DenyCount { get; init; }

    public required IReadOnlyList<QwenToolDescriptor> Tools { get; init; }
}
