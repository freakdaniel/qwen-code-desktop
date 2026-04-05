namespace QwenCode.App.Permissions;

internal sealed record ShellOperation
{
    public required string VirtualTool { get; init; }

    public string? FilePath { get; init; }

    public string? Domain { get; init; }
}
