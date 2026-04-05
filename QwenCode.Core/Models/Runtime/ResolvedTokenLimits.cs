namespace QwenCode.App.Runtime;

public sealed class ResolvedTokenLimits
{
    public required string Model { get; init; }

    public required string NormalizedModel { get; init; }

    public required int InputTokenLimit { get; init; }

    public required int OutputTokenLimit { get; init; }

    public required bool HasExplicitOutputLimit { get; init; }
}
