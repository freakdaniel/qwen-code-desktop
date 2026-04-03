namespace QwenCode.App.Models;

public sealed class InstallExtensionRequest
{
    public required string SourcePath { get; init; }

    public required string InstallMode { get; init; }
}
