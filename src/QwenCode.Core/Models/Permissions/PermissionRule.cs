namespace QwenCode.App.Permissions;

internal sealed class PermissionRule
{
    /// <summary>
    /// Gets or sets the raw
    /// </summary>
    public required string Raw { get; init; }

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the specifier
    /// </summary>
    public string? Specifier { get; init; }

    /// <summary>
    /// Gets or sets the specifier kind
    /// </summary>
    public string? SpecifierKind { get; init; }
}
