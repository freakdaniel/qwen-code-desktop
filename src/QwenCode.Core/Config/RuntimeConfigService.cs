using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Config;

/// <summary>
/// Represents the Runtime Config Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
public sealed class RuntimeConfigService(IDesktopEnvironmentPaths environmentPaths) : IConfigService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting runtime config snapshot</returns>
    public RuntimeConfigSnapshot Inspect(WorkspacePaths paths)
    {
        var projectRoot = string.IsNullOrWhiteSpace(paths.WorkspaceRoot)
            ? environmentPaths.CurrentDirectory
            : Path.GetFullPath(paths.WorkspaceRoot);
        var globalQwenDirectory = Path.Combine(environmentPaths.HomeDirectory, ".qwen");
        var programDataRoot = ResolveProgramDataRoot();
        var systemDefaultsPath = ResolveSystemDefaultsPath(programDataRoot);
        var userSettingsPath = Path.Combine(globalQwenDirectory, "settings.json");
        var projectSettingsPath = Path.Combine(projectRoot, ".qwen", "settings.json");
        var systemSettingsPath = ResolveSystemSettingsPath(programDataRoot);

        var trustLayers = new[]
        {
            CreateLayer("system-defaults", "system-defaults", systemDefaultsPath, included: true),
            CreateLayer("user", "user-settings", userSettingsPath, included: true),
            CreateLayer("system", "system-settings", systemSettingsPath, included: true)
        };
        var trustSettings = BuildMergedSettings(trustLayers);
        var workspaceTrust = ResolveWorkspaceTrust(projectRoot, globalQwenDirectory, trustSettings.FolderTrustEnabled);
        if (!workspaceTrust.IsTrusted && !trustSettings.FolderTrustEnabled)
        {
            workspaceTrust = new WorkspaceTrustDecision(true, string.Empty);
        }

        var settingsLayers = new[]
        {
            CreateLayer("system-defaults", "system-defaults", systemDefaultsPath, included: true),
            CreateLayer("user", "user-settings", userSettingsPath, included: true),
            CreateLayer("project", "project-settings", projectSettingsPath, included: workspaceTrust.IsTrusted),
            CreateLayer("system", "system-settings", systemSettingsPath, included: true)
        };
        var mergedSettings = BuildMergedSettings(settingsLayers);
        var environment = ReadEnvironment(mergedSettings.Root);

        var selectedAuthType = FirstNonEmpty(GetString(mergedSettings.Root, "security", "auth", "selectedType"), "openai");
        var defaultModelName = string.Equals(selectedAuthType, "qwen-oauth", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(selectedAuthType, "qwen_oauth", StringComparison.OrdinalIgnoreCase)
            ? "coder-model"
            : "qwen3-coder-plus";

        return new RuntimeConfigSnapshot
        {
            ProjectRoot = projectRoot,
            GlobalQwenDirectory = globalQwenDirectory,
            ProgramDataRoot = programDataRoot,
            SystemDefaultsPath = systemDefaultsPath,
            UserSettingsPath = userSettingsPath,
            ProjectSettingsPath = projectSettingsPath,
            SystemSettingsPath = systemSettingsPath,
            SettingsLayers = settingsLayers,
            MergedSettings = mergedSettings.Root,
            Environment = environment,
            RuntimeOutputDirectory = mergedSettings.RuntimeOutputDirectory,
            RuntimeSource = mergedSettings.RuntimeSource,
            ModelName = FirstNonEmpty(GetString(mergedSettings.Root, "model", "name"), defaultModelName),
            EmbeddingModel = FirstNonEmpty(GetString(mergedSettings.Root, "embeddingModel"), "text-embedding-v4"),
            SelectedAuthType = selectedAuthType,
            ModelProviders = ParseModelProviders(mergedSettings.Root),
            DefaultApprovalMode = string.IsNullOrWhiteSpace(mergedSettings.DefaultApprovalMode)
                ? "default"
                : mergedSettings.DefaultApprovalMode,
            ConfirmShellCommands = mergedSettings.ConfirmShellCommands,
            ConfirmFileEdits = mergedSettings.ConfirmFileEdits,
            AllowRules = mergedSettings.AllowRules,
            AskRules = mergedSettings.AskRules,
            DenyRules = mergedSettings.DenyRules,
            ContextFileNames = mergedSettings.ContextFileNames.Count > 0 ? mergedSettings.ContextFileNames : ["QWEN.md", "AGENTS.md"],
            FolderTrustEnabled = mergedSettings.FolderTrustEnabled,
            IsWorkspaceTrusted = workspaceTrust.IsTrusted,
            WorkspaceTrustSource = workspaceTrust.Source,
            DisableAllHooks = mergedSettings.DisableAllHooks,
            IdeMode = mergedSettings.IdeMode,
            ListExtensions = mergedSettings.ListExtensions,
            Checkpointing = mergedSettings.Checkpointing,
            OverrideExtensions = mergedSettings.OverrideExtensions,
            AllowedMcpServers = mergedSettings.AllowedMcpServers,
            ExcludedMcpServers = mergedSettings.ExcludedMcpServers,
            ChatCompression = mergedSettings.ChatCompressionThreshold.HasValue
                ? new RuntimeChatCompressionSettings
                {
                    ContextPercentageThreshold = mergedSettings.ChatCompressionThreshold
                }
                : null,
            Telemetry = mergedSettings.TelemetryEnabled ||
                        !string.IsNullOrWhiteSpace(mergedSettings.TelemetryTarget) ||
                        !string.IsNullOrWhiteSpace(mergedSettings.TelemetryOtlpEndpoint) ||
                        !string.IsNullOrWhiteSpace(mergedSettings.TelemetryOutfile)
                ? new RuntimeTelemetrySettings
                {
                    Enabled = mergedSettings.TelemetryEnabled,
                    Target = mergedSettings.TelemetryTarget,
                    OtlpEndpoint = mergedSettings.TelemetryOtlpEndpoint,
                    OtlpProtocol = mergedSettings.TelemetryOtlpProtocol,
                    LogPrompts = mergedSettings.TelemetryLogPrompts,
                    Outfile = mergedSettings.TelemetryOutfile,
                    UseCollector = mergedSettings.TelemetryUseCollector
                }
                : null
        };
    }

    /// <summary>
    /// Resolves settings path
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="scope">The scope</param>
    /// <returns>The resulting string</returns>
    public string ResolveSettingsPath(WorkspacePaths paths, string? scope)
    {
        var snapshot = Inspect(paths);
        return string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase)
            ? snapshot.ProjectSettingsPath
            : snapshot.UserSettingsPath;
    }

    private RuntimeSettingsLayerSnapshot CreateLayer(string scope, string source, string path, bool included) =>
        new()
        {
            Scope = scope,
            Source = source,
            Path = path,
            Included = included
        };

    private MergedSettings BuildMergedSettings(IReadOnlyList<RuntimeSettingsLayerSnapshot> settingsLayers)
    {
        var mergedRoot = new JsonObject();
        string? runtimeOutputDirectory = null;
        string runtimeSource = "default-home";
        string? defaultApprovalMode = null;
        bool? confirmShellCommands = null;
        bool? confirmFileEdits = null;
        IReadOnlyList<string> allowRules = [];
        IReadOnlyList<string> askRules = [];
        IReadOnlyList<string> denyRules = [];
        IReadOnlyList<string> legacyAllowed = [];
        IReadOnlyList<string> legacyCore = [];
        IReadOnlyList<string> legacyExcluded = [];
        IReadOnlyList<string> contextFileNames = [];
        bool folderTrustEnabled = false;
        bool disableAllHooks = false;
        bool ideMode = false;
        bool listExtensions = false;
        bool checkpointing = true;
        IReadOnlyList<string> overrideExtensions = [];
        IReadOnlyList<string> allowedMcpServers = [];
        IReadOnlyList<string> excludedMcpServers = [];
        double? chatCompressionThreshold = null;
        bool telemetryEnabled = false;
        string telemetryTarget = string.Empty;
        string telemetryOtlpEndpoint = string.Empty;
        string telemetryOtlpProtocol = string.Empty;
        bool telemetryLogPrompts = false;
        string telemetryOutfile = string.Empty;
        bool telemetryUseCollector = false;

        foreach (var layer in settingsLayers.Where(static item => item.Included && File.Exists(item.Path)))
        {
            try
            {
                using var stream = File.OpenRead(layer.Path);
                using var document = JsonDocument.Parse(
                    stream,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });
                if (JsonSerializer.SerializeToNode(document.RootElement) is JsonObject layerRoot)
                {
                    MergeObjects(mergedRoot, layerRoot);
                }

                var root = document.RootElement;

                if (TryGetString(root, ["advanced", "runtimeOutputDir"], out var runtimeOutputDir))
                {
                    runtimeOutputDirectory = runtimeOutputDir;
                    runtimeSource = layer.Source;
                }

                if (TryGetString(root, ["permissions", "defaultMode"], out var permissionDefaultMode))
                {
                    defaultApprovalMode = permissionDefaultMode;
                }
                else if (TryGetString(root, ["tools", "approvalMode"], out var legacyApprovalMode))
                {
                    defaultApprovalMode = legacyApprovalMode;
                }

                if (TryGetBoolean(root, ["permissions", "confirmShellCommands"], out var shellConfirm))
                {
                    confirmShellCommands = shellConfirm;
                }

                if (TryGetBoolean(root, ["permissions", "confirmFileEdits"], out var fileEditConfirm))
                {
                    confirmFileEdits = fileEditConfirm;
                }

                if (TryGetStringArray(root, ["permissions", "allow"], out var currentAllowRules))
                {
                    allowRules = currentAllowRules;
                }

                if (TryGetStringArray(root, ["permissions", "ask"], out var currentAskRules))
                {
                    askRules = currentAskRules;
                }

                if (TryGetStringArray(root, ["permissions", "deny"], out var currentDenyRules))
                {
                    denyRules = currentDenyRules;
                }

                if (TryGetStringArray(root, ["tools", "allowed"], out var currentLegacyAllowed))
                {
                    legacyAllowed = currentLegacyAllowed;
                }

                if (TryGetStringArray(root, ["tools", "core"], out var currentLegacyCore))
                {
                    legacyCore = currentLegacyCore;
                }

                if (TryGetStringArray(root, ["tools", "exclude"], out var currentLegacyExcluded))
                {
                    legacyExcluded = currentLegacyExcluded;
                }

                if (TryGetBoolean(root, ["security", "folderTrust", "enabled"], out var currentFolderTrustEnabled))
                {
                    folderTrustEnabled = currentFolderTrustEnabled;
                }

                if (TryGetStringArray(root, ["context", "fileName"], out var currentContextFiles))
                {
                    contextFileNames = currentContextFiles;
                }
                else if (TryGetString(root, ["context", "fileName"], out var singleContextFile) &&
                         !string.IsNullOrWhiteSpace(singleContextFile))
                {
                    contextFileNames = [singleContextFile];
                }

                if (TryGetBoolean(root, ["disableAllHooks"], out var currentDisableAllHooks))
                {
                    disableAllHooks = currentDisableAllHooks;
                }

                if (TryGetBoolean(root, ["ideMode"], out var currentIdeMode))
                {
                    ideMode = currentIdeMode;
                }

                if (TryGetBoolean(root, ["listExtensions"], out var currentListExtensions))
                {
                    listExtensions = currentListExtensions;
                }

                if (TryGetBoolean(root, ["checkpointing"], out var currentCheckpointing))
                {
                    checkpointing = currentCheckpointing;
                }

                if (TryGetStringArray(root, ["overrideExtensions"], out var currentOverrideExtensions))
                {
                    overrideExtensions = currentOverrideExtensions;
                }

                if (TryGetStringArray(root, ["allowedMcpServers"], out var currentAllowedMcpServers))
                {
                    allowedMcpServers = currentAllowedMcpServers;
                }

                if (TryGetStringArray(root, ["excludedMcpServers"], out var currentExcludedMcpServers))
                {
                    excludedMcpServers = currentExcludedMcpServers;
                }

                if (TryGetDouble(root, ["chatCompression", "contextPercentageThreshold"], out var currentChatCompressionThreshold))
                {
                    chatCompressionThreshold = currentChatCompressionThreshold;
                }

                if (TryGetBoolean(root, ["telemetry", "enabled"], out var currentTelemetryEnabled))
                {
                    telemetryEnabled = currentTelemetryEnabled;
                }

                if (TryGetString(root, ["telemetry", "target"], out var currentTelemetryTarget))
                {
                    telemetryTarget = currentTelemetryTarget;
                }

                if (TryGetString(root, ["telemetry", "otlpEndpoint"], out var currentTelemetryOtlpEndpoint))
                {
                    telemetryOtlpEndpoint = currentTelemetryOtlpEndpoint;
                }

                if (TryGetString(root, ["telemetry", "otlpProtocol"], out var currentTelemetryOtlpProtocol))
                {
                    telemetryOtlpProtocol = currentTelemetryOtlpProtocol;
                }

                if (TryGetBoolean(root, ["telemetry", "logPrompts"], out var currentTelemetryLogPrompts))
                {
                    telemetryLogPrompts = currentTelemetryLogPrompts;
                }

                if (TryGetString(root, ["telemetry", "outfile"], out var currentTelemetryOutfile))
                {
                    telemetryOutfile = currentTelemetryOutfile;
                }

                if (TryGetBoolean(root, ["telemetry", "useCollector"], out var currentTelemetryUseCollector))
                {
                    telemetryUseCollector = currentTelemetryUseCollector;
                }
            }
            catch
            {
                // Ignore malformed layers and keep best-effort merged settings.
            }
        }

        return new MergedSettings(
            Root: mergedRoot,
            RuntimeOutputDirectory: runtimeOutputDirectory ?? string.Empty,
            RuntimeSource: runtimeSource,
            DefaultApprovalMode: string.IsNullOrWhiteSpace(defaultApprovalMode) ? "default" : defaultApprovalMode,
            ConfirmShellCommands: confirmShellCommands,
            ConfirmFileEdits: confirmFileEdits,
            AllowRules: MergeRules(allowRules, legacyAllowed, legacyCore),
            AskRules: askRules,
            DenyRules: MergeRules(denyRules, legacyExcluded),
            ContextFileNames: contextFileNames.Count > 0 ? contextFileNames : ["QWEN.md", "AGENTS.md"],
            FolderTrustEnabled: folderTrustEnabled,
            DisableAllHooks: disableAllHooks,
            IdeMode: ideMode,
            ListExtensions: listExtensions,
            Checkpointing: checkpointing,
            OverrideExtensions: overrideExtensions,
            AllowedMcpServers: allowedMcpServers,
            ExcludedMcpServers: excludedMcpServers,
            ChatCompressionThreshold: chatCompressionThreshold,
            TelemetryEnabled: telemetryEnabled,
            TelemetryTarget: telemetryTarget,
            TelemetryOtlpEndpoint: telemetryOtlpEndpoint,
            TelemetryOtlpProtocol: telemetryOtlpProtocol,
            TelemetryLogPrompts: telemetryLogPrompts,
            TelemetryOutfile: telemetryOutfile,
            TelemetryUseCollector: telemetryUseCollector);
    }

    private static IReadOnlyList<RuntimeModelProviderSnapshot> ParseModelProviders(JsonObject mergedSettings)
    {
        if (mergedSettings["modelProviders"] is not JsonObject modelProviders)
        {
            return [];
        }

        var providers = new List<RuntimeModelProviderSnapshot>();
        foreach (var authTypeEntry in modelProviders)
        {
            if (authTypeEntry.Value is not JsonArray authProviders)
            {
                continue;
            }

            foreach (var providerNode in authProviders.OfType<JsonObject>())
            {
                providers.Add(new RuntimeModelProviderSnapshot
                {
                    AuthType = authTypeEntry.Key,
                    Id = providerNode["id"]?.GetValue<string?>() ?? string.Empty,
                    BaseUrl = providerNode["baseUrl"]?.GetValue<string?>() ?? string.Empty,
                    EnvironmentVariableName = providerNode["envKey"]?.GetValue<string?>() ?? string.Empty,
                    ContextWindowSize = ReadInt(providerNode, "generationConfig", "contextWindowSize"),
                    MaxOutputTokens = ReadInt(providerNode, "generationConfig", "maxOutputTokens")
                });
            }
        }

        return providers;
    }

    private static IReadOnlyDictionary<string, string> ReadEnvironment(JsonObject mergedSettings)
    {
        if (mergedSettings["env"] is not JsonObject envObject)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return envObject
            .Where(static pair => pair.Value is JsonValue)
            .Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value?.GetValue<string>()))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value!, StringComparer.OrdinalIgnoreCase);
    }

    private string ResolveProgramDataRoot() =>
        Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH") is { Length: > 0 } overridePath
            ? Path.GetDirectoryName(overridePath) ?? string.Empty
            : environmentPaths.ProgramDataDirectory is { Length: > 0 } commonAppData
                ? Path.Combine(commonAppData, "qwen-code")
                : string.Empty;

    private static string ResolveSystemDefaultsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "system-defaults.json")
            : overridePath;
    }

    private static string ResolveSystemSettingsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "settings.json")
            : overridePath;
    }

    private WorkspaceTrustDecision ResolveWorkspaceTrust(
        string projectRoot,
        string globalQwenDirectory,
        bool folderTrustEnabled)
    {
        if (!folderTrustEnabled)
        {
            return new WorkspaceTrustDecision(true, string.Empty);
        }

        var trustedFoldersPath = Path.Combine(globalQwenDirectory, "trustedFolders.json");
        if (!File.Exists(trustedFoldersPath))
        {
            return new WorkspaceTrustDecision(false, string.Empty);
        }

        try
        {
            using var stream = File.OpenRead(trustedFoldersPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new WorkspaceTrustDecision(false, string.Empty);
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var trustValue = property.Value.GetString();
                if (!IsRecognizedTrustValue(trustValue))
                {
                    continue;
                }

                var normalizedEntry = NormalizePath(property.Name);
                var normalizedProjectRoot = NormalizePath(projectRoot);
                if (string.Equals(trustValue, "DO_NOT_TRUST", StringComparison.OrdinalIgnoreCase) &&
                    PathComparer.Equals(normalizedEntry, normalizedProjectRoot))
                {
                    return new WorkspaceTrustDecision(false, "file");
                }

                if (PathComparer.Equals(normalizedEntry, normalizedProjectRoot))
                {
                    return new WorkspaceTrustDecision(true, "file");
                }

                if (string.Equals(trustValue, "TRUST_PARENT", StringComparison.OrdinalIgnoreCase) &&
                    IsParentPath(normalizedEntry, normalizedProjectRoot))
                {
                    return new WorkspaceTrustDecision(true, "parent");
                }

            }
        }
        catch
        {
            return new WorkspaceTrustDecision(false, string.Empty);
        }

        return new WorkspaceTrustDecision(false, string.Empty);
    }

    private static bool IsRecognizedTrustValue(string? trustValue) =>
        string.Equals(trustValue, "TRUST_FOLDER", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(trustValue, "TRUST_PARENT", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(trustValue, "DO_NOT_TRUST", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private static bool IsParentPath(string parent, string candidate)
    {
        if (PathComparer.Equals(parent, candidate))
        {
            return true;
        }

        var prefix = parent.EndsWith(Path.DirectorySeparatorChar) || parent.EndsWith(Path.AltDirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> MergeRules(params IReadOnlyList<string>[] ruleSets) =>
        ruleSets
            .SelectMany(static ruleSet => ruleSet)
            .Where(static rule => !string.IsNullOrWhiteSpace(rule))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static void MergeObjects(JsonObject target, JsonObject source)
    {
        foreach (var pair in source)
        {
            if (pair.Value is JsonObject sourceObject)
            {
                if (target[pair.Key] is JsonObject targetObject)
                {
                    MergeObjects(targetObject, sourceObject);
                }
                else
                {
                    target[pair.Key] = sourceObject.DeepClone();
                }
            }
            else
            {
                target[pair.Key] = pair.Value?.DeepClone();
            }
        }
    }

    private static bool TryGetString(JsonElement root, IReadOnlyList<string> path, out string value)
    {
        value = string.Empty;
        if (!TryNavigate(root, path, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetBoolean(JsonElement root, IReadOnlyList<string> path, out bool value)
    {
        value = default;
        if (!TryNavigate(root, path, out var element) ||
            (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        value = element.GetBoolean();
        return true;
    }

    private static bool TryGetDouble(JsonElement root, IReadOnlyList<string> path, out double value)
    {
        value = default;
        if (!TryNavigate(root, path, out var element) || element.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return element.TryGetDouble(out value);
    }

    private static bool TryGetStringArray(JsonElement root, IReadOnlyList<string> path, out IReadOnlyList<string> values)
    {
        values = [];
        if (!TryNavigate(root, path, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        values = element.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        return true;
    }

    private static bool TryNavigate(JsonElement element, IReadOnlyList<string> path, out JsonElement result)
    {
        result = element;
        foreach (var segment in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(segment, out result))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetString(JsonObject root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject currentObject || currentObject[segment] is not JsonNode next)
            {
                return string.Empty;
            }

            current = next;
        }

        return current?.GetValue<string?>() ?? string.Empty;
    }

    private static int? ReadInt(JsonObject root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject currentObject || currentObject[segment] is not JsonNode next)
            {
                return null;
            }

            current = next;
        }

        return current is JsonValue value && value.TryGetValue<int>(out var result)
            ? result
            : null;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record WorkspaceTrustDecision(bool IsTrusted, string Source);

    private sealed record MergedSettings(
        JsonObject Root,
        string RuntimeOutputDirectory,
        string RuntimeSource,
        string DefaultApprovalMode,
        bool? ConfirmShellCommands,
        bool? ConfirmFileEdits,
        IReadOnlyList<string> AllowRules,
        IReadOnlyList<string> AskRules,
        IReadOnlyList<string> DenyRules,
        IReadOnlyList<string> ContextFileNames,
        bool FolderTrustEnabled,
        bool DisableAllHooks,
        bool IdeMode,
        bool ListExtensions,
        bool Checkpointing,
        IReadOnlyList<string> OverrideExtensions,
        IReadOnlyList<string> AllowedMcpServers,
        IReadOnlyList<string> ExcludedMcpServers,
        double? ChatCompressionThreshold,
        bool TelemetryEnabled,
        string TelemetryTarget,
        string TelemetryOtlpEndpoint,
        string TelemetryOtlpProtocol,
        bool TelemetryLogPrompts,
        string TelemetryOutfile,
        bool TelemetryUseCollector);
}
