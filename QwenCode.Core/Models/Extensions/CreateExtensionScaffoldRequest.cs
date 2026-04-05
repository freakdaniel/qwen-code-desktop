namespace QwenCode.App.Models;

public sealed class CreateExtensionScaffoldRequest
{
    public required string TargetPath { get; init; }

    public string Template { get; init; } = string.Empty;
}
