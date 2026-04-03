namespace QwenCode.App.Models;

public sealed class QwenRuntimeProfile
{
    public required string ProjectRoot { get; init; }

    public required string GlobalQwenDirectory { get; init; }

    public required string RuntimeBaseDirectory { get; init; }

    public required string RuntimeSource { get; init; }

    public required string ProjectDataDirectory { get; init; }

    public required string ChatsDirectory { get; init; }

    public required string HistoryDirectory { get; init; }

    public required IReadOnlyList<string> ContextFileNames { get; init; }

    public required IReadOnlyList<string> ContextFilePaths { get; init; }

    public bool FolderTrustEnabled { get; init; }

    public bool IsWorkspaceTrusted { get; init; }

    public string WorkspaceTrustSource { get; init; } = string.Empty;

    public required ApprovalProfile ApprovalProfile { get; init; }
}
