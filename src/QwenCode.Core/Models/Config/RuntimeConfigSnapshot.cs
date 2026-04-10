using System.Text.Json.Nodes;

namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Runtime Config Snapshot
/// </summary>
public sealed class RuntimeConfigSnapshot
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
    /// Gets or sets the program data root
    /// </summary>
    public required string ProgramDataRoot { get; init; }

    /// <summary>
    /// Gets or sets the system defaults path
    /// </summary>
    public required string SystemDefaultsPath { get; init; }

    /// <summary>
    /// Gets or sets the user settings path
    /// </summary>
    public required string UserSettingsPath { get; init; }

    /// <summary>
    /// Gets or sets the project settings path
    /// </summary>
    public required string ProjectSettingsPath { get; init; }

    /// <summary>
    /// Gets or sets the system settings path
    /// </summary>
    public required string SystemSettingsPath { get; init; }

    /// <summary>
    /// Gets or sets the settings layers
    /// </summary>
    public required IReadOnlyList<RuntimeSettingsLayerSnapshot> SettingsLayers { get; init; }

    /// <summary>
    /// Gets or sets the merged settings
    /// </summary>
    public required JsonObject MergedSettings { get; init; }

    /// <summary>
    /// Gets or sets the environment
    /// </summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>
    /// Gets or sets the runtime output directory
    /// </summary>
    public string RuntimeOutputDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime source
    /// </summary>
    public string RuntimeSource { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the embedding model
    /// </summary>
    public string EmbeddingModel { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected auth type
    /// </summary>
    public string SelectedAuthType { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model providers
    /// </summary>
    public required IReadOnlyList<RuntimeModelProviderSnapshot> ModelProviders { get; init; }

    /// <summary>
    /// Gets or sets the default approval mode
    /// </summary>
    public string DefaultApprovalMode { get; init; } = "default";

    /// <summary>
    /// Gets or sets the confirm shell commands
    /// </summary>
    public bool? ConfirmShellCommands { get; init; }

    /// <summary>
    /// Gets or sets the confirm file edits
    /// </summary>
    public bool? ConfirmFileEdits { get; init; }

    /// <summary>
    /// Gets or sets the allow rules
    /// </summary>
    public required IReadOnlyList<string> AllowRules { get; init; }

    /// <summary>
    /// Gets or sets the ask rules
    /// </summary>
    public required IReadOnlyList<string> AskRules { get; init; }

    /// <summary>
    /// Gets or sets the deny rules
    /// </summary>
    public required IReadOnlyList<string> DenyRules { get; init; }

    /// <summary>
    /// Gets or sets the context file names
    /// </summary>
    public required IReadOnlyList<string> ContextFileNames { get; init; }

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
    /// Gets or sets the disable all hooks
    /// </summary>
    public bool DisableAllHooks { get; init; }

    /// <summary>
    /// Gets or sets the ide mode
    /// </summary>
    public bool IdeMode { get; init; }

    /// <summary>
    /// Gets or sets the list extensions
    /// </summary>
    public bool ListExtensions { get; init; }

    /// <summary>
    /// Gets or sets the checkpointing
    /// </summary>
    public bool Checkpointing { get; init; }

    /// <summary>
    /// Gets or sets the override extensions
    /// </summary>
    public required IReadOnlyList<string> OverrideExtensions { get; init; }

    /// <summary>
    /// Gets or sets the allowed mcp servers
    /// </summary>
    public required IReadOnlyList<string> AllowedMcpServers { get; init; }

    /// <summary>
    /// Gets or sets the excluded mcp servers
    /// </summary>
    public required IReadOnlyList<string> ExcludedMcpServers { get; init; }

    /// <summary>
    /// Gets or sets the chat compression
    /// </summary>
    public RuntimeChatCompressionSettings? ChatCompression { get; init; }

    /// <summary>
    /// Gets or sets the telemetry
    /// </summary>
    public RuntimeTelemetrySettings? Telemetry { get; init; }
}
