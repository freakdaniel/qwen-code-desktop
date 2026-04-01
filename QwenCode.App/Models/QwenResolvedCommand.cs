namespace QwenCode.App.Models;

public sealed class QwenResolvedCommand
{
    public required string Name { get; init; }

    public required string Scope { get; init; }

    public required string SourcePath { get; init; }

    public required string Description { get; init; }

    public required string Arguments { get; init; }

    public required string ResolvedPrompt { get; init; }
}
