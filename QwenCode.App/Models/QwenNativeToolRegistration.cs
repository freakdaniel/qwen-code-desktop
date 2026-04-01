namespace QwenCode.App.Models;

public sealed class QwenNativeToolRegistration
{
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required string Kind { get; init; }

    public bool IsImplemented { get; init; }

    public required string ApprovalState { get; init; }

    public required string ApprovalReason { get; init; }
}
