using System.Security.Cryptography;
using System.Text.Json;
using QwenCode.App.Models;
using QwenCode.App.Infrastructure;

namespace QwenCode.App.Compatibility;

public sealed class QwenRuntimeProfileService(IDesktopEnvironmentPaths environmentPaths)
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public QwenRuntimeProfile Inspect(WorkspacePaths paths)
    {
        var projectRoot = string.IsNullOrWhiteSpace(paths.WorkspaceRoot)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(paths.WorkspaceRoot);
        var globalQwenDirectory = Path.Combine(environmentPaths.HomeDirectory, ".qwen");

        var projectSettingsPath = Path.Combine(projectRoot, ".qwen", "settings.json");
        var userSettingsPath = Path.Combine(globalQwenDirectory, "settings.json");
        var systemRoot = ResolveProgramDataRoot();
        var systemDefaultsPath = GetSystemDefaultsPath(systemRoot);
        var systemSettingsPath = GetSystemSettingsPath(systemRoot);

        var mergedSettings = BuildMergedSettings(
            [
                systemDefaultsPath,
                userSettingsPath,
                projectSettingsPath,
                systemSettingsPath
            ]);

        var runtimeSource = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QWEN_RUNTIME_DIR"))
            ? "environment"
            : mergedSettings.RuntimeSource;
        var runtimeDirectory = ResolveRuntimeBaseDirectory(globalQwenDirectory, projectRoot, mergedSettings.RuntimeOutputDirectory);
        var projectDataDirectory = Path.Combine(runtimeDirectory, "projects", SanitizePath(projectRoot));
        var historyDirectory = Path.Combine(runtimeDirectory, "history", ComputeProjectHash(projectRoot));
        var contextFileNames = mergedSettings.ContextFileNames.Count > 0
            ? mergedSettings.ContextFileNames
            : ["QWEN.md", "AGENTS.md"];

        return new QwenRuntimeProfile
        {
            ProjectRoot = projectRoot,
            GlobalQwenDirectory = globalQwenDirectory,
            RuntimeBaseDirectory = runtimeDirectory,
            RuntimeSource = runtimeSource,
            ProjectDataDirectory = projectDataDirectory,
            ChatsDirectory = Path.Combine(projectDataDirectory, "chats"),
            HistoryDirectory = historyDirectory,
            ContextFileNames = contextFileNames,
            ContextFilePaths = contextFileNames
                .Select(fileName => Path.Combine(projectRoot, fileName))
                .ToArray(),
            ApprovalProfile = new ApprovalProfile
            {
                DefaultMode = mergedSettings.DefaultApprovalMode,
                ConfirmShellCommands = mergedSettings.ConfirmShellCommands,
                ConfirmFileEdits = mergedSettings.ConfirmFileEdits,
                AllowRules = mergedSettings.AllowRules,
                AskRules = mergedSettings.AskRules,
                DenyRules = mergedSettings.DenyRules
            }
        };
    }

    private static MergedQwenSettings BuildMergedSettings(
        IReadOnlyList<string> settingsPaths)
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

        foreach (var path in settingsPaths.Where(File.Exists))
        {
            try
            {
                using var stream = File.OpenRead(path);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                if (TryGetString(root, ["advanced", "runtimeOutputDir"], out var runtimeOutputDir))
                {
                    runtimeOutputDirectory = runtimeOutputDir;
                    runtimeSource = PathComparer.Equals(path, settingsPaths[0])
                        ? "system-defaults"
                        : PathComparer.Equals(path, settingsPaths[1])
                            ? "user-settings"
                            : PathComparer.Equals(path, settingsPaths[2])
                                ? "project-settings"
                                : "system-settings";
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
            ContextFileNames: contextFileNames.Count > 0 ? contextFileNames : ["QWEN.md", "AGENTS.md"]);
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

    private sealed record MergedQwenSettings(
        string? RuntimeOutputDirectory,
        string RuntimeSource,
        string DefaultApprovalMode,
        bool? ConfirmShellCommands,
        bool? ConfirmFileEdits,
        IReadOnlyList<string> AllowRules,
        IReadOnlyList<string> AskRules,
        IReadOnlyList<string> DenyRules,
        IReadOnlyList<string> ContextFileNames);
}
