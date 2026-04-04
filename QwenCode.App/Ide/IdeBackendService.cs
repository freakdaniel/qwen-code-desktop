using System.Text.Json;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public sealed class IdeBackendService(
    IDesktopEnvironmentPaths environmentPaths,
    IIdeDetectionService detectionService,
    IIdeContextService contextService,
    IIdeInstallerService installerService,
    IIdeProcessProbe processProbe) : IIdeBackendService
{
    public IdeConnectionSnapshot Inspect(string workspaceRoot, string processCommand = "")
    {
        var config = GetBestConnectionConfig(workspaceRoot);
        var ide = detectionService.Detect(processCommand, overrideInfo: config?.Ide);
        var validation = ValidateWorkspacePath(config?.WorkspacePath, workspaceRoot);

        return new IdeConnectionSnapshot
        {
            Status = validation.IsValid && config is not null ? "connected" : "disconnected",
            Details = validation.Error ?? (config is null ? "IDE companion connection was not found." : string.Empty),
            Ide = ide,
            WorkspacePath = config?.WorkspacePath ?? string.Empty,
            Port = config?.Port ?? string.Empty,
            Command = config?.StdioCommand ?? string.Empty,
            AuthToken = string.IsNullOrWhiteSpace(config?.AuthToken) ? string.Empty : "***",
            SupportsDiff = config?.AvailableTools.Contains("openDiff", StringComparer.OrdinalIgnoreCase) == true,
            AvailableTools = config?.AvailableTools ?? [],
            Context = contextService.Get()
        };
    }

    public IdeContextSnapshot UpdateContext(IdeContextSnapshot snapshot)
    {
        contextService.Set(snapshot);
        return contextService.Get()!;
    }

    public Task<IdeInstallResult> InstallCompanionAsync(IdeInfo ide, CancellationToken cancellationToken = default) =>
        installerService.InstallCompanionAsync(ide, cancellationToken);

    internal static WorkspaceValidationResult ValidateWorkspacePath(string? ideWorkspacePath, string cwd)
    {
        if (ideWorkspacePath is null)
        {
            return WorkspaceValidationResult.Invalid(
                "Failed to connect to IDE companion extension. Please ensure the extension is running. To install the extension, run /ide install.");
        }

        if (string.IsNullOrWhiteSpace(ideWorkspacePath))
        {
            return WorkspaceValidationResult.Invalid(
                "To use IDE integration, open a workspace folder in the IDE and try again.");
        }

        var paths = ideWorkspacePath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fullCwd = Path.GetFullPath(cwd);
        var isWithinWorkspace = paths.Any(path =>
        {
            var fullPath = Path.GetFullPath(path);
            return fullCwd.StartsWith(fullPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        });

        return isWithinWorkspace
            ? WorkspaceValidationResult.Valid()
            : WorkspaceValidationResult.Invalid(
                $"Directory mismatch. Run Qwen Code from one of the IDE workspaces: {string.Join(", ", paths)}");
    }

    private IdeConnectionConfigRecord? GetBestConnectionConfig(string workspaceRoot)
    {
        var ideRoot = Path.Combine(environmentPaths.HomeDirectory, ".qwen", "ide");
        var fileBackedConfig = Directory.Exists(ideRoot)
            ? Directory.GetFiles(ideRoot, "*.lock", SearchOption.TopDirectoryOnly)
                .Select(ReadConnectionConfig)
                .Where(static item => item is not null)
                .Cast<IdeConnectionConfigRecord>()
                .Where(config => !config.ParentProcessId.HasValue || processProbe.Exists(config.ParentProcessId.Value))
                .OrderByDescending(static item => item.LastModifiedUtc)
                .FirstOrDefault(config => ValidateWorkspacePath(config.WorkspacePath, workspaceRoot).IsValid)
            : null;
        if (fileBackedConfig is not null)
        {
            return fileBackedConfig;
        }

        var fallbackConfig = GetConfigFromEnvironmentOrLegacy();
        if (fallbackConfig is not null)
        {
            return fallbackConfig;
        }

        if (!Directory.Exists(ideRoot))
        {
            return null;
        }

        return Directory.GetFiles(ideRoot, "*.lock", SearchOption.TopDirectoryOnly)
            .Select(ReadConnectionConfig)
            .Where(static item => item is not null)
            .Cast<IdeConnectionConfigRecord>()
            .Where(config => !config.ParentProcessId.HasValue || processProbe.Exists(config.ParentProcessId.Value))
            .OrderByDescending(static item => item.LastModifiedUtc)
            .FirstOrDefault();
    }

    private static IdeConnectionConfigRecord? ReadConnectionConfig(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new IdeConnectionConfigRecord
            {
                Port = ReadString(root, "port"),
                AuthToken = ReadString(root, "authToken"),
                WorkspacePath = ReadString(root, "workspacePath"),
                StdioCommand = root.TryGetProperty("stdio", out var stdio) && stdio.ValueKind == JsonValueKind.Object
                    ? ReadString(stdio, "command")
                    : string.Empty,
                AvailableTools = root.TryGetProperty("availableTools", out var tools) && tools.ValueKind == JsonValueKind.Array
                    ? tools.EnumerateArray()
                        .Where(static item => item.ValueKind == JsonValueKind.String)
                        .Select(static item => item.GetString() ?? string.Empty)
                        .Where(static item => !string.IsNullOrWhiteSpace(item))
                        .ToArray()
                    : [],
                Ide = root.TryGetProperty("ideInfo", out var ideInfo) &&
                      ideInfo.ValueKind == JsonValueKind.Object &&
                      !string.IsNullOrWhiteSpace(ReadString(ideInfo, "name")) &&
                      !string.IsNullOrWhiteSpace(ReadString(ideInfo, "displayName"))
                    ? new IdeInfo
                    {
                        Name = ReadString(ideInfo, "name"),
                        DisplayName = ReadString(ideInfo, "displayName")
                    }
                    : null,
                ParentProcessId = root.TryGetProperty("ppid", out var ppid) && ppid.ValueKind == JsonValueKind.Number
                    ? ppid.GetInt32()
                    : null,
                LastModifiedUtc = File.GetLastWriteTimeUtc(path)
            };
        }
        catch
        {
            return null;
        }
    }

    private IdeConnectionConfigRecord? GetConfigFromEnvironmentOrLegacy()
    {
        var envPort = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_SERVER_PORT");
        if (!string.IsNullOrWhiteSpace(envPort))
        {
            var envLockPath = Path.Combine(environmentPaths.HomeDirectory, ".qwen", "ide", $"{envPort}.lock");
            var lockConfig = ReadConnectionConfig(envLockPath);
            if (lockConfig is not null)
            {
                return lockConfig;
            }

            var legacyPath = Path.Combine(Path.GetTempPath(), $"qwen-code-ide-server-{envPort}.json");
            var legacyConfig = ReadConnectionConfig(legacyPath);
            if (legacyConfig is not null)
            {
                return legacyConfig;
            }
        }

        var stdioCommand = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_SERVER_STDIO_COMMAND");
        var stdioArgs = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_SERVER_STDIO_ARGS");
        var workspacePath = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_WORKSPACE_PATH");
        var authToken = Environment.GetEnvironmentVariable("QWEN_CODE_IDE_AUTH_TOKEN");
        if (string.IsNullOrWhiteSpace(envPort) &&
            string.IsNullOrWhiteSpace(stdioCommand) &&
            string.IsNullOrWhiteSpace(workspacePath) &&
            string.IsNullOrWhiteSpace(authToken))
        {
            return null;
        }

        return new IdeConnectionConfigRecord
        {
            Port = envPort ?? string.Empty,
            AuthToken = authToken ?? string.Empty,
            WorkspacePath = workspacePath ?? string.Empty,
            StdioCommand = string.IsNullOrWhiteSpace(stdioCommand)
                ? string.Empty
                : string.IsNullOrWhiteSpace(stdioArgs)
                    ? stdioCommand
                    : $"{stdioCommand} {stdioArgs}",
            LastModifiedUtc = DateTime.UtcNow
        };
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private sealed class IdeConnectionConfigRecord
    {
        public string Port { get; init; } = string.Empty;

        public string AuthToken { get; init; } = string.Empty;

        public string WorkspacePath { get; init; } = string.Empty;

        public string StdioCommand { get; init; } = string.Empty;

        public IdeInfo? Ide { get; init; }

        public int? ParentProcessId { get; init; }

        public DateTime LastModifiedUtc { get; init; }

        public IReadOnlyList<string> AvailableTools { get; init; } = [];
    }

    internal sealed record WorkspaceValidationResult(bool IsValid, string? Error)
    {
        public static WorkspaceValidationResult Valid() => new(true, null);

        public static WorkspaceValidationResult Invalid(string error) => new(false, error);
    }
}
