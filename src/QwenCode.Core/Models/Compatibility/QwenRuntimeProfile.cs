namespace QwenCode.App.Models;

/// <summary>
/// Represents the Qwen Runtime Profile
/// </summary>
public sealed class QwenRuntimeProfile
{
    /// <summary>
    /// Gets or sets the project root
    /// </summary>
    public required string ProjectRoot { get; init; }

    /// <summary>
    /// Gets or sets the global qwen directory
    /// </summary>
    public required string GlobalQwenDirectory { get; init; }

    /// <summary>
    /// Gets or sets the runtime base directory
    /// </summary>
    public required string RuntimeBaseDirectory { get; init; }

    /// <summary>
    /// Gets or sets the runtime source
    /// </summary>
    public required string RuntimeSource { get; init; }

    /// <summary>
    /// Gets or sets the project data directory
    /// </summary>
    public required string ProjectDataDirectory { get; init; }

    /// <summary>
    /// Gets or sets the chats directory
    /// </summary>
    public required string ChatsDirectory { get; init; }

    /// <summary>
    /// Gets or sets the history directory
    /// </summary>
    public required string HistoryDirectory { get; init; }

    /// <summary>
    /// Gets or sets the context file names
    /// </summary>
    public required IReadOnlyList<string> ContextFileNames { get; init; }

    /// <summary>
    /// Gets or sets the context file paths
    /// </summary>
    public required IReadOnlyList<string> ContextFilePaths { get; init; }

    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the embedding model
    /// </summary>
    public string EmbeddingModel { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the preferred locale
    /// </summary>
    public string CurrentLocale { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the preferred language name
    /// </summary>
    public string CurrentLanguage { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the chat compression
    /// </summary>
    public RuntimeChatCompressionSettings? ChatCompression { get; init; }

    /// <summary>
    /// Gets or sets the telemetry
    /// </summary>
    public RuntimeTelemetrySettings? Telemetry { get; init; }

    /// <summary>
    /// Gets or sets the checkpointing
    /// </summary>
    public bool Checkpointing { get; init; } = true;

    /// <summary>
    /// Gets or sets the folder trust enabled
    /// </summary>
    public bool FolderTrustEnabled { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is workspace trusted
    /// </summary>
    public bool IsWorkspaceTrusted { get; init; }

    /// <summary>
    /// Gets or sets the workspace trust source
    /// </summary>
    public string WorkspaceTrustSource { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the approval profile
    /// </summary>
    public required ApprovalProfile ApprovalProfile { get; init; }
}
