using System.Security.Cryptography;
using System.Text.Json;
using QwenCode.App.Config;
using QwenCode.App.Models;
using QwenCode.App.Infrastructure;
using QwenCode.App.Runtime;

namespace QwenCode.App.Compatibility;

/// <summary>
/// Represents the Qwen Runtime Profile Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="configService">The config service</param>
public sealed class QwenRuntimeProfileService(
    IDesktopEnvironmentPaths environmentPaths,
    IConfigService? configService = null)
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly IConfigService config = configService ?? new RuntimeConfigService(environmentPaths);

    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting qwen runtime profile</returns>
    public QwenRuntimeProfile Inspect(WorkspacePaths paths)
    {
        var snapshot = config.Inspect(paths);
        var runtimeSource = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QWEN_RUNTIME_DIR"))
            ? "environment"
            : snapshot.RuntimeSource;
        var runtimeDirectory = ResolveRuntimeBaseDirectory(snapshot.GlobalQwenDirectory, snapshot.ProjectRoot, snapshot.RuntimeOutputDirectory);
        var projectDataDirectory = Path.Combine(runtimeDirectory, "projects", SanitizePath(snapshot.ProjectRoot));
        var historyDirectory = Path.Combine(runtimeDirectory, "history", ComputeProjectHash(snapshot.ProjectRoot));
        var contextFileNames = snapshot.ContextFileNames.Count > 0
            ? snapshot.ContextFileNames
            : ["QWEN.md", "AGENTS.md"];
        var locale = RuntimeLocaleCatalog.DetectLocale();

        return new QwenRuntimeProfile
        {
            ProjectRoot = snapshot.ProjectRoot,
            GlobalQwenDirectory = snapshot.GlobalQwenDirectory,
            RuntimeBaseDirectory = runtimeDirectory,
            RuntimeSource = runtimeSource,
            ProjectDataDirectory = projectDataDirectory,
            ChatsDirectory = Path.Combine(projectDataDirectory, "chats"),
            HistoryDirectory = historyDirectory,
            ContextFileNames = contextFileNames,
            ContextFilePaths = contextFileNames
                .Select(fileName => Path.Combine(snapshot.ProjectRoot, fileName))
                .ToArray(),
            ModelName = snapshot.ModelName,
            EmbeddingModel = snapshot.EmbeddingModel,
            CurrentLocale = locale,
            CurrentLanguage = RuntimeLocaleCatalog.ResolveLanguageName(locale),
            ChatCompression = snapshot.ChatCompression,
            Telemetry = snapshot.Telemetry,
            Checkpointing = snapshot.Checkpointing,
            FolderTrustEnabled = snapshot.FolderTrustEnabled,
            IsWorkspaceTrusted = snapshot.IsWorkspaceTrusted,
            WorkspaceTrustSource = snapshot.WorkspaceTrustSource,
            ApprovalProfile = new ApprovalProfile
            {
                DefaultMode = snapshot.DefaultApprovalMode,
                ConfirmShellCommands = snapshot.ConfirmShellCommands,
                ConfirmFileEdits = snapshot.ConfirmFileEdits,
                AllowRules = snapshot.AllowRules,
                AskRules = snapshot.AskRules,
                DenyRules = snapshot.DenyRules
            }
        };
    }

    private static MergedQwenSettings BuildMergedSettings(
        IReadOnlyList<SettingsLayer> settingsLayers)
    {
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

        foreach (var layer in settingsLayers.Where(static item => File.Exists(item.Path)))
        {
            try
            {
                using var stream = File.OpenRead(layer.Path);
                using var document = JsonDocument.Parse(stream);
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

                if (TryGetBoolean(root, ["permissions", "confirmShellCommands"], out var shellConfirmValue))
                {
                    confirmShellCommands = shellConfirmValue;
                }

                if (TryGetBoolean(root, ["permissions", "confirmFileEdits"], out var editConfirmValue))
                {
                    confirmFileEdits = editConfirmValue;
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
            }
            catch
            {
                // Ignore malformed settings layers and continue with the remaining ones.
            }
        }

        return new MergedQwenSettings(
            RuntimeOutputDirectory: runtimeOutputDirectory,
            RuntimeSource: runtimeSource,
            DefaultApprovalMode: string.IsNullOrWhiteSpace(defaultApprovalMode) ? "default" : defaultApprovalMode,
            ConfirmShellCommands: confirmShellCommands,
            ConfirmFileEdits: confirmFileEdits,
            AllowRules: MergeRules(allowRules, legacyAllowed, legacyCore),
            AskRules: askRules,
            DenyRules: MergeRules(denyRules, legacyExcluded),
            ContextFileNames: contextFileNames.Count > 0 ? contextFileNames : ["QWEN.md", "AGENTS.md"],
            FolderTrustEnabled: folderTrustEnabled);
    }

    private static IReadOnlyList<string> MergeRules(params IReadOnlyList<string>[] ruleSets) =>
        ruleSets
            .SelectMany(static ruleSet => ruleSet)
            .Where(static rule => !string.IsNullOrWhiteSpace(rule))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

    private static string ResolveRuntimeBaseDirectory(
        string globalQwenDirectory,
        string projectRoot,
        string? runtimeOutputDirectory)
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("QWEN_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return ResolvePath(runtimeDirectory, projectRoot);
        }

        return string.IsNullOrWhiteSpace(runtimeOutputDirectory)
            ? globalQwenDirectory
            : ResolvePath(runtimeOutputDirectory, projectRoot);
    }

    private string ResolveProgramDataRoot() =>
        Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH") is { Length: > 0 } overridePath
            ? Path.GetDirectoryName(overridePath) ?? string.Empty
            : environmentPaths.ProgramDataDirectory is { Length: > 0 } commonAppData
                ? Path.Combine(commonAppData, "qwen-code")
                : string.Empty;

    private static string GetSystemDefaultsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "system-defaults.json")
            : overridePath;
    }

    private static string GetSystemSettingsPath(string programDataRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(programDataRoot, "settings.json")
            : overridePath;
    }

    private static string ResolvePath(string path, string projectRoot)
    {
        var expandedPath = path switch
        {
            "~" => GetHomeDirectory(projectRoot),
            _ when path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal)
                => Path.Combine(GetHomeDirectory(projectRoot), path[2..]),
            _ => path
        };

        return Path.IsPathRooted(expandedPath)
            ? Path.GetFullPath(expandedPath)
            : Path.GetFullPath(Path.Combine(projectRoot, expandedPath));

        static string GetHomeDirectory(string projectRootPath) =>
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is { Length: > 0 } homeDirectory
                ? homeDirectory
                : Path.GetPathRoot(projectRootPath) ?? projectRootPath;
    }

    private static string SanitizePath(string path)
    {
        var normalizedPath = OperatingSystem.IsWindows() ? path.ToLowerInvariant() : path;
        return new string(normalizedPath.Select(static character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray());
    }

    private static string ComputeProjectHash(string path)
    {
        var normalizedPath = OperatingSystem.IsWindows() ? path.ToLowerInvariant() : path;
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedPath));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static WorkspaceTrustDecision ResolveWorkspaceTrust(
        string projectRoot,
        string globalQwenDirectory,
        bool folderTrustEnabled)
    {
        if (!folderTrustEnabled)
        {
            return new WorkspaceTrustDecision(true, string.Empty);
        }

        var trustedFoldersPath = Environment.GetEnvironmentVariable("QWEN_CODE_TRUSTED_FOLDERS_PATH");
        if (string.IsNullOrWhiteSpace(trustedFoldersPath))
        {
            trustedFoldersPath = Path.Combine(globalQwenDirectory, "trustedFolders.json");
        }

        if (!File.Exists(trustedFoldersPath))
        {
            return new WorkspaceTrustDecision(true, string.Empty);
        }

        try
        {
            using var stream = File.OpenRead(trustedFoldersPath);
            using var document = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new WorkspaceTrustDecision(true, string.Empty);
            }

            var normalizedProjectRoot = NormalizePath(projectRoot);
            var trustedPaths = new List<string>();
            var untrustedPaths = new List<string>();

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var configuredPath = NormalizePath(property.Name);
                switch (property.Value.GetString())
                {
                    case "TRUST_FOLDER":
                        trustedPaths.Add(configuredPath);
                        break;
                    case "TRUST_PARENT":
                        trustedPaths.Add(NormalizePath(Path.GetDirectoryName(configuredPath) ?? configuredPath));
                        break;
                    case "DO_NOT_TRUST":
                        untrustedPaths.Add(configuredPath);
                        break;
                }
            }

            foreach (var trustedPath in trustedPaths)
            {
                if (IsWithinRoot(normalizedProjectRoot, trustedPath))
                {
                    return new WorkspaceTrustDecision(true, "file");
                }
            }

            foreach (var untrustedPath in untrustedPaths)
            {
                if (PathComparer.Equals(normalizedProjectRoot, untrustedPath))
                {
                    return new WorkspaceTrustDecision(false, "file");
                }
            }

            return new WorkspaceTrustDecision(true, string.Empty);
        }
        catch
        {
            return new WorkspaceTrustDecision(true, string.Empty);
        }
    }

    private static bool IsWithinRoot(string location, string root)
    {
        if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        if (PathComparer.Equals(location, root))
        {
            return true;
        }

        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = $"{normalizedRoot}{Path.DirectorySeparatorChar}";
        return location.StartsWith(rootWithSeparator, StringComparisonFromComparer(PathComparer));
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static StringComparison StringComparisonFromComparer(StringComparer comparer) =>
        comparer == StringComparer.OrdinalIgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private sealed record MergedQwenSettings(
        string? RuntimeOutputDirectory,
        string RuntimeSource,
        string DefaultApprovalMode,
        bool? ConfirmShellCommands,
        bool? ConfirmFileEdits,
        IReadOnlyList<string> AllowRules,
        IReadOnlyList<string> AskRules,
        IReadOnlyList<string> DenyRules,
        IReadOnlyList<string> ContextFileNames,
        bool FolderTrustEnabled);

    private sealed record SettingsLayer(string Path, string Source);

    private sealed record WorkspaceTrustDecision(bool IsTrusted, string Source);
}
