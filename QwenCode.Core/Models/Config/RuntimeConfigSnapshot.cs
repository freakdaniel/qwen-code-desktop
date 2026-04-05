using System.Text.Json.Nodes;

namespace QwenCode.App.Models;

public sealed class RuntimeConfigSnapshot
{
    public required string ProjectRoot { get; init; }

    public required string GlobalQwenDirectory { get; init; }

    public required string ProgramDataRoot { get; init; }

    public required string SystemDefaultsPath { get; init; }

    public required string UserSettingsPath { get; init; }

    public required string ProjectSettingsPath { get; init; }

    public required string SystemSettingsPath { get; init; }

    public required IReadOnlyList<RuntimeSettingsLayerSnapshot> SettingsLayers { get; init; }

    public required JsonObject MergedSettings { get; init; }

    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    public string RuntimeOutputDirectory { get; init; } = string.Empty;

    public string RuntimeSource { get; init; } = string.Empty;

    public string ModelName { get; init; } = string.Empty;

    public string EmbeddingModel { get; init; } = string.Empty;

    public string SelectedAuthType { get; init; } = string.Empty;

    public required IReadOnlyList<RuntimeModelProviderSnapshot> ModelProviders { get; init; }

    public string DefaultApprovalMode { get; init; } = "default";

    public bool? ConfirmShellCommands { get; init; }

    public bool? ConfirmFileEdits { get; init; }

    public required IReadOnlyList<string> AllowRules { get; init; }

    public required IReadOnlyList<string> AskRules { get; init; }

    public required IReadOnlyList<string> DenyRules { get; init; }

    public required IReadOnlyList<string> ContextFileNames { get; init; }

    public bool FolderTrustEnabled { get; init; }

    public bool IsWorkspaceTrusted { get; init; }

    public string WorkspaceTrustSource { get; init; } = string.Empty;

    public bool DisableAllHooks { get; init; }

    public bool IdeMode { get; init; }

    public bool ListExtensions { get; init; }

    public bool Checkpointing { get; init; }

    public required IReadOnlyList<string> OverrideExtensions { get; init; }

    public required IReadOnlyList<string> AllowedMcpServers { get; init; }

    public required IReadOnlyList<string> ExcludedMcpServers { get; init; }

    public RuntimeChatCompressionSettings? ChatCompression { get; init; }

    public RuntimeTelemetrySettings? Telemetry { get; init; }
}
