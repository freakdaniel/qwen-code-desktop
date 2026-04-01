namespace QwenCode.App.Permissions;

internal sealed class PermissionRule
{
    public required string Raw { get; init; }

    public required string ToolName { get; init; }

    public string? Specifier { get; init; }

    public string? SpecifierKind { get; init; }
}
