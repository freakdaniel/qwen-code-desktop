using System.Text.Json;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Ide;

/// <summary>
/// Represents the Ide Backend Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="detectionService">The detection service</param>
/// <param name="contextService">The context service</param>
/// <param name="installerService">The installer service</param>
/// <param name="processProbe">The process probe</param>
public sealed class IdeBackendService(
    IDesktopEnvironmentPaths environmentPaths,
    IIdeDetectionService detectionService,
    IIdeContextService contextService,
    IIdeInstallerService installerService,
    IIdeProcessProbe processProbe) : IIdeBackendService
{
    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="workspaceRoot">The workspace root</param>
    /// <param name="processCommand">The process command</param>
    /// <returns>The resulting ide connection snapshot</returns>
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
            Command = BuildCommandPreview(config),
            AuthToken = string.IsNullOrWhiteSpace(config?.AuthToken) ? string.Empty : "***",
            SupportsDiff = config?.AvailableTools.Contains("openDiff", StringComparer.OrdinalIgnoreCase) == true,
            AvailableTools = config?.AvailableTools ?? [],
            Context = contextService.Get()
        };
    }

    /// <summary>
    /// Resolves transport connection
    /// </summary>
    /// <param name="workspaceRoot">The workspace root</param>
    /// <param name="processCommand">The process command</param>
    /// <returns>The resulting ide transport connection info?</returns>
    public IdeTransportConnectionInfo? ResolveTransportConnection(string workspaceRoot, string processCommand = "")
    {
        var config = GetBestConnectionConfig(workspaceRoot);
        var validation = ValidateWorkspacePath(config?.WorkspacePath, workspaceRoot);
        if (!validation.IsValid || config is null)
        {
            return null;
        }

        return new IdeTransportConnectionInfo
        {
            WorkspacePath = config.WorkspacePath,
            Port = config.Port,
            AuthToken = config.AuthToken,
            StdioCommand = config.StdioCommand,
            StdioArguments = config.StdioArguments
        };
    }

    /// <summary>
    /// Updates context
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    /// <returns>The resulting ide context snapshot</returns>
    public IdeContextSnapshot UpdateContext(IdeContextSnapshot snapshot)
    {
        contextService.Set(snapshot);
        return contextService.Get()!;
    }

    /// <summary>
    /// Executes install companion async
    /// </summary>
    /// <param name="ide">The ide</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide install result</returns>
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
                StdioArguments = root.TryGetProperty("stdio", out stdio) && stdio.ValueKind == JsonValueKind.Object
                                 && stdio.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array
                    ? args.EnumerateArray()
                        .Where(static item => item.ValueKind == JsonValueKind.String)
                        .Select(static item => item.GetString() ?? string.Empty)
                        .Where(static item => !string.IsNullOrWhiteSpace(item))
                        .ToArray()
                    : [],
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
            StdioCommand = stdioCommand ?? string.Empty,
            StdioArguments = ParseCommandArguments(stdioArgs),
            LastModifiedUtc = DateTime.UtcNow
        };
    }

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string BuildCommandPreview(IdeConnectionConfigRecord? config)
    {
        if (config is null || string.IsNullOrWhiteSpace(config.StdioCommand))
        {
            return string.Empty;
        }

        return config.StdioArguments.Count == 0
            ? config.StdioCommand
            : $"{config.StdioCommand} {string.Join(' ', config.StdioArguments)}";
    }

    private static IReadOnlyList<string> ParseCommandArguments(string? rawArgs)
    {
        if (string.IsNullOrWhiteSpace(rawArgs))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(rawArgs);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return document.RootElement.EnumerateArray()
                    .Where(static item => item.ValueKind == JsonValueKind.String)
                    .Select(static item => item.GetString() ?? string.Empty)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .ToArray();
            }
        }
        catch
        {
        }

        return SplitCommandLine(rawArgs);
    }

    private static IReadOnlyList<string> SplitCommandLine(string raw)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var character in raw)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private sealed class IdeConnectionConfigRecord
    {
        /// <summary>
        /// Gets or sets the port
        /// </summary>
        public string Port { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the auth token
        /// </summary>
        public string AuthToken { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the workspace path
        /// </summary>
        public string WorkspacePath { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the stdio command
        /// </summary>
        public string StdioCommand { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the stdio arguments
        /// </summary>
        public IReadOnlyList<string> StdioArguments { get; init; } = [];

        /// <summary>
        /// Gets or sets the ide
        /// </summary>
        public IdeInfo? Ide { get; init; }

        /// <summary>
        /// Gets or sets the parent process id
        /// </summary>
        public int? ParentProcessId { get; init; }

        /// <summary>
        /// Gets or sets the last modified utc
        /// </summary>
        public DateTime LastModifiedUtc { get; init; }

        /// <summary>
        /// Gets or sets the available tools
        /// </summary>
        public IReadOnlyList<string> AvailableTools { get; init; } = [];
    }

    internal sealed record WorkspaceValidationResult(bool IsValid, string? Error)
    {
        /// <summary>
        /// Executes valid
        /// </summary>
        /// <returns>The resulting workspace validation result</returns>
        public static WorkspaceValidationResult Valid() => new(true, null);

        /// <summary>
        /// Executes invalid
        /// </summary>
        /// <param name="error">The error</param>
        /// <returns>The resulting workspace validation result</returns>
        public static WorkspaceValidationResult Invalid(string error) => new(false, error);
    }
}
