namespace QwenCode.App.Permissions;

/// <summary>
/// Represents the Approval Check Context
/// </summary>
public sealed record ApprovalCheckContext
{
    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the kind
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Gets or sets the project root
    /// </summary>
    public string? ProjectRoot { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the command
    /// </summary>
    public string? Command { get; init; }

    /// <summary>
    /// Gets or sets the file path
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets or sets the domain
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Gets or sets the specifier
    /// </summary>
    public string? Specifier { get; init; }
}
