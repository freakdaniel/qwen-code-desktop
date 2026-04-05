namespace QwenCode.App.Permissions;

internal sealed record ShellOperation
{
    /// <summary>
    /// Gets or sets the virtual tool
    /// </summary>
    public required string VirtualTool { get; init; }

    /// <summary>
    /// Gets or sets the file path
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets or sets the domain
    /// </summary>
    public string? Domain { get; init; }
}
