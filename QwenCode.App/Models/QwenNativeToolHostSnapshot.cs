namespace QwenCode.App.Models;

public sealed class QwenNativeToolHostSnapshot
{
    public int RegisteredCount { get; init; }

    public int ImplementedCount { get; init; }

    public int ReadyCount { get; init; }

    public int ApprovalRequiredCount { get; init; }

    public required IReadOnlyList<QwenNativeToolRegistration> Tools { get; init; }
}
