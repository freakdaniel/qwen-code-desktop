namespace QwenCode.App.Models;

public sealed class ToolDescriptor
{
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string Kind { get; init; }

    public required string SourcePath { get; init; }

    public required string ApprovalState { get; init; }

    public required string ApprovalReason { get; init; }
}
