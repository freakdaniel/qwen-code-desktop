namespace QwenCode.App.Permissions;

public sealed record ApprovalCheckContext
{
    public required string ToolName { get; init; }

    public required string Kind { get; init; }

    public string? ProjectRoot { get; init; }

    public string? WorkingDirectory { get; init; }

    public string? Command { get; init; }

    public string? FilePath { get; init; }

    public string? Domain { get; init; }

    public string? Specifier { get; init; }
}
