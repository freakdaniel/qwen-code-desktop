using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using QwenCode.App.Compatibility;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Extensions;

public sealed partial class ExtensionCatalogService(
    QwenRuntimeProfileService runtimeProfileService,
    IDesktopEnvironmentPaths environmentPaths) : IExtensionCatalogService
{
    private const string ManifestFileName = "qwen-extension.json";
    private const string InstallMetadataFileName = ".qwen-extension-install.json";
    private const string EnablementFileName = "extension-enablement.json";
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    public ExtensionSnapshot Inspect(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        var enablementConfig = ReadEnablementConfig(extensionsDirectory);

        var extensions = Directory.Exists(extensionsDirectory)
            ? Directory.EnumerateDirectories(extensionsDirectory)
                .Select(wrapperPath => TryLoadExtensionDefinition(wrapperPath, runtimeProfile, enablementConfig))
                .Where(static extension => extension is not null)
                .Cast<ExtensionDefinition>()
                .OrderBy(static extension => extension.Name, NameComparer)
                .ToArray()
            : [];

        return BuildSnapshot(extensions);
    }

    public IReadOnlyList<CommandHookConfiguration> ListActiveHooks(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        if (!runtimeProfile.IsWorkspaceTrusted)
        {
            return [];
        }

        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        var enablementConfig = ReadEnablementConfig(extensionsDirectory);
        if (!Directory.Exists(extensionsDirectory))
        {
            return [];
        }

        var hooks = new List<CommandHookConfiguration>();
        foreach (var wrapperPath in Directory.EnumerateDirectories(extensionsDirectory))
        {
            try
            {
                var metadata = ReadInstallMetadata(wrapperPath);
                var effectivePath = ResolveEffectivePath(wrapperPath, metadata);
                if (!Directory.Exists(effectivePath))
                {
                    continue;
                }

                var manifest = LoadManifest(effectivePath);
                var workspaceEnabled = IsEnabled(manifest.Name, runtimeProfile.ProjectRoot, enablementConfig);
                if (!workspaceEnabled)
                {
                    continue;
                }

                hooks.AddRange(manifest.Hooks);
            }
            catch
            {
                // Invalid extensions should not block hook discovery.
            }
        }

        return hooks;
    }

    public ExtensionSettingsSnapshot GetSettings(WorkspacePaths paths, GetExtensionSettingsRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        var wrapperPath = FindExtensionWrapperPath(extensionsDirectory, request.Name)
                          ?? throw new InvalidOperationException($"Extension \"{request.Name}\" was not found.");
        var metadata = ReadInstallMetadata(wrapperPath);
        var effectivePath = ResolveEffectivePath(wrapperPath, metadata);
        var manifest = LoadManifest(effectivePath);
        var userValues = ReadScopedSettings(manifest, GetUserSettingsPath(wrapperPath), GetUserSecretsPath(wrapperPath));
        var workspaceValues = ReadScopedSettings(
            manifest,
            GetWorkspaceSettingsPath(runtimeProfile, manifest.Name),
            GetWorkspaceSecretsPath(runtimeProfile, manifest.Name));

        var settings = manifest.Settings
            .Select(setting =>
            {
                var hasWorkspaceValue = workspaceValues.TryGetValue(setting.EnvironmentVariable, out var workspaceValue);
                var hasUserValue = userValues.TryGetValue(setting.EnvironmentVariable, out var userValue);

                return new ExtensionSettingValue
                {
                    Name = setting.Name,
                    Description = setting.Description,
                    EnvironmentVariable = setting.EnvironmentVariable,
                    Sensitive = setting.Sensitive,
                    UserValue = hasUserValue ? userValue! : string.Empty,
                    WorkspaceValue = hasWorkspaceValue ? workspaceValue! : string.Empty,
                    EffectiveValue = hasWorkspaceValue
                        ? workspaceValue!
                        : hasUserValue
                            ? userValue!
                            : string.Empty,
                    HasUserValue = hasUserValue,
                    HasWorkspaceValue = hasWorkspaceValue
                };
            })
            .ToArray();

        return new ExtensionSettingsSnapshot
        {
            ExtensionName = manifest.Name,
            Version = manifest.Version,
            InstallType = metadata?.Type ?? "local",
            Path = effectivePath,
            Settings = settings
        };
    }

    public ExtensionSnapshot Install(WorkspacePaths paths, InstallExtensionRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        Directory.CreateDirectory(extensionsDirectory);

        var sourcePath = Path.GetFullPath(request.SourcePath);
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Extension source directory was not found: {sourcePath}");
        }

        var manifest = LoadManifest(sourcePath);
        var wrapperPath = Path.Combine(extensionsDirectory, SanitizeDirectoryName(manifest.Name));

        if (Directory.Exists(wrapperPath))
        {
            Directory.Delete(wrapperPath, recursive: true);
        }

        if (string.Equals(request.InstallMode, "link", StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(wrapperPath);
        }
        else if (string.Equals(request.InstallMode, "copy", StringComparison.OrdinalIgnoreCase))
        {
            CopyDirectory(sourcePath, wrapperPath);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported extension install mode: {request.InstallMode}");
        }

        var metadata = new ExtensionInstallMetadata
        {
            Source = sourcePath,
            Type = string.Equals(request.InstallMode, "link", StringComparison.OrdinalIgnoreCase) ? "link" : "local"
        };
        WriteJson(Path.Combine(wrapperPath, InstallMetadataFileName), JsonSerializer.SerializeToNode(metadata) ?? new JsonObject());

        return Inspect(paths);
    }

    public ExtensionSettingsSnapshot SetSetting(WorkspacePaths paths, SetExtensionSettingValueRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        var wrapperPath = FindExtensionWrapperPath(extensionsDirectory, request.Name)
                          ?? throw new InvalidOperationException($"Extension \"{request.Name}\" was not found.");
        var metadata = ReadInstallMetadata(wrapperPath);
        var effectivePath = ResolveEffectivePath(wrapperPath, metadata);
        var manifest = LoadManifest(effectivePath);
        var definition = manifest.Settings.FirstOrDefault(setting =>
            NameComparer.Equals(setting.Name, request.Setting) ||
            NameComparer.Equals(setting.EnvironmentVariable, request.Setting));

        if (definition is null)
        {
            throw new InvalidOperationException(
                $"Setting \"{request.Setting}\" was not found for extension \"{request.Name}\".");
        }

        var envPath = string.Equals(request.Scope, "project", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(request.Scope, "workspace", StringComparison.OrdinalIgnoreCase)
            ? GetWorkspaceSettingsPath(runtimeProfile, manifest.Name)
            : GetUserSettingsPath(wrapperPath);
        var secretPath = string.Equals(request.Scope, "project", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(request.Scope, "workspace", StringComparison.OrdinalIgnoreCase)
            ? GetWorkspaceSecretsPath(runtimeProfile, manifest.Name)
            : GetUserSecretsPath(wrapperPath);

        if (definition.Sensitive)
        {
            var secrets = ReadSecretFile(secretPath);
            secrets[definition.EnvironmentVariable] = request.Value;
            WriteSecretFile(secretPath, secrets);
        }
        else
        {
            var values = ReadEnvFile(envPath);
            values[definition.EnvironmentVariable] = request.Value;
            WriteEnvFile(envPath, values);
        }

        return GetSettings(paths, new GetExtensionSettingsRequest
        {
            Name = request.Name
        });
    }

    public ExtensionSnapshot SetEnabled(WorkspacePaths paths, SetExtensionEnabledRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        Directory.CreateDirectory(extensionsDirectory);

        var config = new Dictionary<string, ExtensionEnablementConfig>(ReadEnablementConfig(extensionsDirectory), NameComparer);
        var scopePath = ResolveScopePath(runtimeProfile, request.Scope);

        if (request.Enabled)
        {
            EnableByPath(config, request.Name, includeSubdirectories: true, scopePath);
        }
        else
        {
            DisableByPath(config, request.Name, includeSubdirectories: true, scopePath);
        }

        WriteEnablementConfig(extensionsDirectory, config);
        return Inspect(paths);
    }

    public ExtensionSnapshot Remove(WorkspacePaths paths, RemoveExtensionRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        if (!Directory.Exists(extensionsDirectory))
        {
            return Inspect(paths);
        }

        var targetWrapper = FindExtensionWrapperPath(extensionsDirectory, request.Name);
        if (targetWrapper is null)
        {
            throw new InvalidOperationException($"Extension \"{request.Name}\" was not found.");
        }

        Directory.Delete(targetWrapper, recursive: true);
        RemoveEnablementConfig(extensionsDirectory, request.Name);
        return Inspect(paths);
    }

    private ExtensionDefinition? TryLoadExtensionDefinition(
        string wrapperPath,
        QwenRuntimeProfile runtimeProfile,
        IReadOnlyDictionary<string, ExtensionEnablementConfig> enablementConfig)
    {
        try
        {
            var metadata = ReadInstallMetadata(wrapperPath);
            var effectivePath = ResolveEffectivePath(wrapperPath, metadata);

            if (!Directory.Exists(effectivePath))
            {
                return BuildMissingLinkedExtension(wrapperPath, metadata);
            }

            var manifest = LoadManifest(effectivePath);
            var userEnabled = IsEnabled(manifest.Name, environmentPaths.HomeDirectory, enablementConfig);
            var workspaceEnabled = runtimeProfile.IsWorkspaceTrusted &&
                                   IsEnabled(manifest.Name, runtimeProfile.ProjectRoot, enablementConfig);

            return new ExtensionDefinition
            {
                Name = manifest.Name,
                Version = manifest.Version,
                Description = manifest.Description,
                Path = effectivePath,
                WrapperPath = wrapperPath,
                Status = !runtimeProfile.IsWorkspaceTrusted
                    ? "workspace-untrusted"
                    : workspaceEnabled
                        ? "active"
                        : "disabled",
                InstallType = metadata?.Type ?? "local",
                Source = metadata?.Source ?? effectivePath,
                UserEnabled = userEnabled,
                WorkspaceEnabled = workspaceEnabled,
                IsActive = workspaceEnabled,
                ContextFiles = manifest.ContextFiles,
                Commands = DiscoverCommands(effectivePath, manifest.CommandRoots),
                Skills = DiscoverSkills(effectivePath, manifest.SkillRoots),
                Agents = DiscoverAgents(effectivePath, manifest.AgentRoots),
                McpServers = manifest.McpServers,
                Channels = manifest.Channels,
                SettingsCount = manifest.SettingsCount,
                HookEventCount = manifest.HookEventCount
            };
        }
        catch (Exception exception)
        {
            return new ExtensionDefinition
            {
                Name = Path.GetFileName(wrapperPath),
                Version = "unknown",
                Description = string.Empty,
                Path = wrapperPath,
                WrapperPath = wrapperPath,
                Status = "invalid",
                InstallType = "unknown",
                Source = wrapperPath,
                UserEnabled = false,
                WorkspaceEnabled = false,
                IsActive = false,
                LastError = exception.Message
            };
        }
    }

    private static ExtensionDefinition BuildMissingLinkedExtension(string wrapperPath, ExtensionInstallMetadata? metadata) =>
        new()
        {
            Name = Path.GetFileName(wrapperPath),
            Version = "unknown",
            Description = string.Empty,
            Path = metadata?.Source ?? wrapperPath,
            WrapperPath = wrapperPath,
            Status = "missing-source",
            InstallType = metadata?.Type ?? "link",
            Source = metadata?.Source ?? string.Empty,
            UserEnabled = false,
            WorkspaceEnabled = false,
            IsActive = false,
            LastError = "Linked extension source directory is missing."
        };

    private static ExtensionSnapshot BuildSnapshot(IReadOnlyList<ExtensionDefinition> extensions) =>
        new()
        {
            TotalCount = extensions.Count,
            ActiveCount = extensions.Count(static extension => extension.IsActive),
            LinkedCount = extensions.Count(static extension => string.Equals(extension.InstallType, "link", StringComparison.OrdinalIgnoreCase)),
            MissingCount = extensions.Count(static extension =>
                string.Equals(extension.Status, "missing-source", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension.Status, "invalid", StringComparison.OrdinalIgnoreCase)),
            Extensions = extensions
        };

    private static string GetExtensionsDirectory(QwenRuntimeProfile runtimeProfile) =>
        Path.Combine(runtimeProfile.GlobalQwenDirectory, "extensions");

    private string ResolveScopePath(QwenRuntimeProfile runtimeProfile, string scope) =>
        string.Equals(scope, "workspace", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase)
            ? runtimeProfile.ProjectRoot
            : environmentPaths.HomeDirectory;

    private static string ResolveEffectivePath(string wrapperPath, ExtensionInstallMetadata? metadata) =>
        metadata is { Type: not null, Source: { Length: > 0 } source } &&
        string.Equals(metadata.Type, "link", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(source)
            : wrapperPath;

    private static ExtensionInstallMetadata? ReadInstallMetadata(string wrapperPath)
    {
        var metadataPath = Path.Combine(wrapperPath, InstallMetadataFileName);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ExtensionInstallMetadata>(File.ReadAllText(metadataPath));
        }
        catch
        {
            return null;
        }
    }

    private static ExtensionManifest LoadManifest(string extensionPath)
    {
        var manifestPath = Path.Combine(extensionPath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Extension manifest was not found at {manifestPath}");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(manifestPath));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to parse extension manifest at {manifestPath}: {exception.Message}", exception);
        }

        if (root is not JsonObject manifestObject)
        {
            throw new InvalidOperationException($"Extension manifest at {manifestPath} must be a JSON object.");
        }

        var name = manifestObject["name"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException($"Extension manifest at {manifestPath} must declare a non-empty name.");
        }

        var version = manifestObject["version"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            version = "0.0.0";
        }

        return new ExtensionManifest(
            Name: name,
            Version: version,
            Description: manifestObject["description"]?.GetValue<string>()?.Trim() ?? string.Empty,
            ContextFiles: ParseStringList(manifestObject["contextFileName"], fallback: ["QWEN.md"]),
            CommandRoots: ParseStringList(manifestObject["commands"]),
            SkillRoots: ParseStringList(manifestObject["skills"]),
            AgentRoots: ParseStringList(manifestObject["agents"]),
            Settings: ParseSettings(manifestObject["settings"]),
            Hooks: ParseHooks(manifestObject["hooks"]),
            McpServers: ParsePropertyNames(manifestObject["mcpServers"]),
            Channels: ParsePropertyNames(manifestObject["channels"]),
            SettingsCount: ParseSettings(manifestObject["settings"]).Count,
            HookEventCount: ParseObjectPropertyCount(manifestObject["hooks"]));
    }

    private static IReadOnlyList<ExtensionSettingDefinition> ParseSettings(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return [];
        }

        return array
            .OfType<JsonObject>()
            .Select(setting =>
            {
                var name = setting["name"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var description = setting["description"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var envVar = setting["envVar"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var sensitive = setting["sensitive"]?.GetValue<bool>() ?? false;

                return string.IsNullOrWhiteSpace(envVar) || string.IsNullOrWhiteSpace(name)
                    ? null
                    : new ExtensionSettingDefinition
                    {
                        Name = name,
                        Description = description,
                        EnvironmentVariable = envVar,
                        Sensitive = sensitive
                    };
            })
            .OfType<ExtensionSettingDefinition>()
            .ToArray();
    }

    private static IReadOnlyList<CommandHookConfiguration> ParseHooks(JsonNode? node)
    {
        if (node is not JsonObject objectNode)
        {
            return [];
        }

        var hooks = new List<CommandHookConfiguration>();
        foreach (var property in objectNode)
        {
            if (property.Value is not JsonArray definitions ||
                !Enum.TryParse<HookEventName>(property.Key, ignoreCase: false, out var eventName))
            {
                continue;
            }

            foreach (var definitionNode in definitions.OfType<JsonObject>())
            {
                var matcher = definitionNode["matcher"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var sequential = definitionNode["sequential"]?.GetValue<bool>() ?? false;
                if (definitionNode["hooks"] is not JsonArray hookArray)
                {
                    continue;
                }

                foreach (var hookNode in hookArray.OfType<JsonObject>())
                {
                    var type = hookNode["type"]?.GetValue<string>()?.Trim();
                    var command = hookNode["command"]?.GetValue<string>()?.Trim();
                    if (!string.Equals(type, "command", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(command))
                    {
                        continue;
                    }

                    hooks.Add(new CommandHookConfiguration
                    {
                        Command = command,
                        Name = hookNode["name"]?.GetValue<string>()?.Trim() ?? string.Empty,
                        Matcher = matcher,
                        Description = hookNode["description"]?.GetValue<string>()?.Trim() ?? string.Empty,
                        TimeoutMs = hookNode["timeout"]?.GetValue<int?>() is { } timeout && timeout > 0 ? timeout : 60_000,
                        EnvironmentVariables = ParseEnvironmentVariables(hookNode["env"]),
                        Source = HookConfigSource.Extensions,
                        EventName = eventName,
                        Sequential = sequential
                    });
                }
            }
        }

        return hooks;
    }

    private static IReadOnlyDictionary<string, string> ParseEnvironmentVariables(JsonNode? node)
    {
        if (node is not JsonObject objectNode)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return objectNode
            .Where(static pair => pair.Value is JsonValue)
            .Select(static pair => (pair.Key, Value: pair.Value!.GetValue<string>()))
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ParseStringList(JsonNode? node, IReadOnlyList<string>? fallback = null)
    {
        if (node is null)
        {
            return fallback ?? [];
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var stringValue) && !string.IsNullOrWhiteSpace(stringValue))
        {
            return [stringValue.Trim()];
        }

        if (node is JsonArray array)
        {
            return array
                .Select(static item => item?.GetValue<string>())
                .OfType<string>()
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Select(static item => item.Trim())
                .Distinct(NameComparer)
                .ToArray();
        }

        return fallback ?? [];
    }

    private static IReadOnlyList<string> ParsePropertyNames(JsonNode? node) =>
        node is JsonObject obj
            ? obj.Select(static pair => pair.Key)
                .OrderBy(static item => item, NameComparer)
                .ToArray()
            : [];

    private static int ParseArrayCount(JsonNode? node) => node is JsonArray array ? array.Count : 0;

    private static int ParseObjectPropertyCount(JsonNode? node) => node is JsonObject obj ? obj.Count : 0;

    private static IReadOnlyList<string> DiscoverCommands(string extensionPath, IReadOnlyList<string> commandRoots)
    {
        if (commandRoots.Count == 0)
        {
            return [];
        }

        var results = new List<string>();
        foreach (var commandRoot in commandRoots)
        {
            var fullRoot = ResolveChildPath(extensionPath, commandRoot);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(fullRoot, "*.md", SearchOption.AllDirectories))
            {
                var relativePath = Path.ChangeExtension(Path.GetRelativePath(fullRoot, path), null) ?? string.Empty;
                var commandName = string.Join(
                    ':',
                    relativePath
                        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Where(static segment => !string.IsNullOrWhiteSpace(segment))
                        .Select(static segment => segment.Replace(':', '_')));

                if (!string.IsNullOrWhiteSpace(commandName))
                {
                    results.Add(commandName);
                }
            }
        }

        return results.Distinct(NameComparer).OrderBy(static item => item, NameComparer).ToArray();
    }

    private static IReadOnlyList<string> DiscoverSkills(string extensionPath, IReadOnlyList<string> skillRoots)
    {
        if (skillRoots.Count == 0)
        {
            return [];
        }

        var results = new List<string>();
        foreach (var skillRoot in skillRoots)
        {
            var fullRoot = ResolveChildPath(extensionPath, skillRoot);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var skillDirectory in Directory.EnumerateDirectories(fullRoot))
            {
                var skillPath = Path.Combine(skillDirectory, "SKILL.md");
                if (!File.Exists(skillPath))
                {
                    continue;
                }

                var explicitName = TryReadFrontmatterValue(skillPath, "name");
                results.Add(string.IsNullOrWhiteSpace(explicitName) ? Path.GetFileName(skillDirectory) : explicitName);
            }
        }

        return results.Distinct(NameComparer).OrderBy(static item => item, NameComparer).ToArray();
    }

    private static IReadOnlyList<string> DiscoverAgents(string extensionPath, IReadOnlyList<string> agentRoots)
    {
        if (agentRoots.Count == 0)
        {
            return [];
        }

        var results = new List<string>();
        foreach (var agentRoot in agentRoots)
        {
            var fullRoot = ResolveChildPath(extensionPath, agentRoot);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(fullRoot))
            {
                if (HasAgentMarkdown(directory))
                {
                    results.Add(Path.GetFileName(directory));
                }
            }
        }

        return results.Distinct(NameComparer).OrderBy(static item => item, NameComparer).ToArray();
    }

    private static string ResolveChildPath(string basePath, string candidate) =>
        Path.IsPathRooted(candidate)
            ? Path.GetFullPath(candidate)
            : Path.GetFullPath(Path.Combine(basePath, candidate));

    private static Dictionary<string, string> ReadScopedSettings(
        ExtensionManifest manifest,
        string envPath,
        string secretPath)
    {
        var values = ReadEnvFile(envPath);
        foreach (var pair in ReadSecretFile(secretPath))
        {
            values[pair.Key] = pair.Value;
        }

        return values
            .Where(pair => manifest.Settings.Any(setting => NameComparer.Equals(setting.EnvironmentVariable, pair.Key)))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, NameComparer);
    }

    private static Dictionary<string, string> ReadEnvFile(string path)
    {
        var result = new Dictionary<string, string>(NameComparer);
        if (!File.Exists(path))
        {
            return result;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static void WriteEnvFile(string path, IReadOnlyDictionary<string, string> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = values
            .OrderBy(static pair => pair.Key, NameComparer)
            .Select(static pair =>
            {
                var needsQuotes = pair.Value.Contains(' ', StringComparison.Ordinal);
                var value = needsQuotes ? $"\"{pair.Value}\"" : pair.Value;
                return $"{pair.Key}={value}";
            });
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static Dictionary<string, string> ReadSecretFile(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(NameComparer);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                       File.ReadAllText(path),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                   new Dictionary<string, string>(NameComparer);
        }
        catch
        {
            return new Dictionary<string, string>(NameComparer);
        }
    }

    private static void WriteSecretFile(string path, IReadOnlyDictionary<string, string> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(values, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }

    private static bool HasAgentMarkdown(string directory) =>
        File.Exists(Path.Combine(directory, "AGENT.md")) ||
        File.Exists(Path.Combine(directory, "agent.md")) ||
        File.Exists(Path.Combine(directory, "SUBAGENT.md")) ||
        File.Exists(Path.Combine(directory, "subagent.md"));

    private static string TryReadFrontmatterValue(string markdownPath, string key)
    {
        try
        {
            var content = File.ReadAllText(markdownPath);
            var match = FrontmatterRegex().Match(content.Replace("\r\n", "\n", StringComparison.Ordinal));
            if (!match.Success)
            {
                return string.Empty;
            }

            foreach (var rawLine in match.Groups["yaml"].Value.Split('\n'))
            {
                var line = rawLine.Trim();
                if (!line.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return line[(key.Length + 1)..].Trim().Trim('"');
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string? FindExtensionWrapperPath(string extensionsDirectory, string name)
    {
        foreach (var wrapperPath in Directory.EnumerateDirectories(extensionsDirectory))
        {
            if (NameComparer.Equals(Path.GetFileName(wrapperPath), name))
            {
                return wrapperPath;
            }

            var metadata = ReadInstallMetadata(wrapperPath);
            var effectivePath = ResolveEffectivePath(wrapperPath, metadata);
            var manifestPath = Path.Combine(effectivePath, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifestName = JsonNode.Parse(File.ReadAllText(manifestPath))?["name"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(manifestName) && NameComparer.Equals(manifestName, name))
                {
                    return wrapperPath;
                }
            }
            catch
            {
                // Ignore malformed entries during lookup.
            }
        }

        return null;
    }

    private static string SanitizeDirectoryName(string name)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(character => invalidCharacters.Contains(character) ? '-' : character));
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static IReadOnlyDictionary<string, ExtensionEnablementConfig> ReadEnablementConfig(string extensionsDirectory)
    {
        var path = Path.Combine(extensionsDirectory, EnablementFileName);
        if (!File.Exists(path))
        {
            return new Dictionary<string, ExtensionEnablementConfig>(NameComparer);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ExtensionEnablementConfig>>(
                       File.ReadAllText(path),
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       }) ??
                   new Dictionary<string, ExtensionEnablementConfig>(NameComparer);
        }
        catch
        {
            return new Dictionary<string, ExtensionEnablementConfig>(NameComparer);
        }
    }

    private static void WriteEnablementConfig(
        string extensionsDirectory,
        IReadOnlyDictionary<string, ExtensionEnablementConfig> config)
    {
        Directory.CreateDirectory(extensionsDirectory);
        WriteJson(Path.Combine(extensionsDirectory, EnablementFileName), JsonSerializer.SerializeToNode(config) ?? new JsonObject());
    }

    private static void RemoveEnablementConfig(string extensionsDirectory, string extensionName)
    {
        var config = new Dictionary<string, ExtensionEnablementConfig>(ReadEnablementConfig(extensionsDirectory), NameComparer);
        var matchingKey = config.Keys.FirstOrDefault(key => NameComparer.Equals(key, extensionName));
        if (!string.IsNullOrWhiteSpace(matchingKey))
        {
            config.Remove(matchingKey);
            WriteEnablementConfig(extensionsDirectory, config);
        }
    }

    private static bool IsEnabled(
        string extensionName,
        string currentPath,
        IReadOnlyDictionary<string, ExtensionEnablementConfig> config)
    {
        var matchingKey = config.Keys.FirstOrDefault(key => NameComparer.Equals(key, extensionName));
        var overrides = matchingKey is null ? [] : config[matchingKey].Overrides;
        var enabled = true;
        var normalizedPath = EnsureLeadingAndTrailingSlash(currentPath);

        foreach (var rule in overrides)
        {
            var parsed = ExtensionOverride.FromFileRule(rule);
            if (parsed.MatchesPath(normalizedPath))
            {
                enabled = !parsed.IsDisable;
            }
        }

        return enabled;
    }

    private static void EnableByPath(
        IDictionary<string, ExtensionEnablementConfig> config,
        string extensionName,
        bool includeSubdirectories,
        string scopePath)
    {
        var key = ResolveConfigKey(config, extensionName);
        if (!config.TryGetValue(key, out var existingConfig))
        {
            existingConfig = new ExtensionEnablementConfig();
            config[key] = existingConfig;
        }

        var desiredOverride = ExtensionOverride.FromInput(scopePath, includeSubdirectories);
        var retainedOverrides = existingConfig.Overrides
            .Select(ExtensionOverride.FromFileRule)
            .Where(current => !current.ConflictsWith(desiredOverride) &&
                              !current.IsEqualTo(desiredOverride) &&
                              !current.IsChildOf(desiredOverride))
            .Select(static item => item.Output())
            .ToList();

        retainedOverrides.Add(desiredOverride.Output());
        existingConfig.Overrides = retainedOverrides;
    }

    private static void DisableByPath(
        IDictionary<string, ExtensionEnablementConfig> config,
        string extensionName,
        bool includeSubdirectories,
        string scopePath) =>
        EnableByPath(config, extensionName, includeSubdirectories, $"!{scopePath}");

    private static string ResolveConfigKey(IDictionary<string, ExtensionEnablementConfig> config, string extensionName)
    {
        foreach (var key in config.Keys)
        {
            if (NameComparer.Equals(key, extensionName))
            {
                return key;
            }
        }

        return extensionName;
    }

    private static void WriteJson(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static string EnsureLeadingAndTrailingSlash(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        return normalized;
    }

    [GeneratedRegex("^---\\n(?<yaml>[\\s\\S]*?)\\n---(?:\\n|$)", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    private static string GetUserSettingsPath(string wrapperPath) =>
        Path.Combine(wrapperPath, ".env");

    private static string GetUserSecretsPath(string wrapperPath) =>
        Path.Combine(wrapperPath, ".qwen-extension-secrets.json");

    private static string GetWorkspaceSettingsPath(QwenRuntimeProfile runtimeProfile, string extensionName) =>
        Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "extensions", SanitizeDirectoryName(extensionName), ".env");

    private static string GetWorkspaceSecretsPath(QwenRuntimeProfile runtimeProfile, string extensionName) =>
        Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "extensions", SanitizeDirectoryName(extensionName), ".qwen-extension-secrets.json");

    private sealed record ExtensionManifest(
        string Name,
        string Version,
        string Description,
        IReadOnlyList<string> ContextFiles,
        IReadOnlyList<string> CommandRoots,
        IReadOnlyList<string> SkillRoots,
        IReadOnlyList<string> AgentRoots,
        IReadOnlyList<ExtensionSettingDefinition> Settings,
        IReadOnlyList<CommandHookConfiguration> Hooks,
        IReadOnlyList<string> McpServers,
        IReadOnlyList<string> Channels,
        int SettingsCount,
        int HookEventCount);

    private sealed class ExtensionEnablementConfig
    {
        public List<string> Overrides { get; set; } = [];
    }

    private sealed class ExtensionOverride(string baseRule, bool isDisable, bool includeSubdirectories)
    {
        public string BaseRule { get; } = baseRule;

        public bool IsDisable { get; } = isDisable;

        public bool IncludeSubdirectories { get; } = includeSubdirectories;

        public static ExtensionOverride FromInput(string inputRule, bool includeSubdirectories)
        {
            var disable = inputRule.StartsWith('!');
            var baseRule = disable ? inputRule[1..] : inputRule;
            return new ExtensionOverride(EnsureLeadingAndTrailingSlash(baseRule), disable, includeSubdirectories);
        }

        public static ExtensionOverride FromFileRule(string fileRule)
        {
            var disable = fileRule.StartsWith('!');
            var baseRule = disable ? fileRule[1..] : fileRule;
            var includeChildren = baseRule.EndsWith('*');
            if (includeChildren)
            {
                baseRule = baseRule[..^1];
            }

            return new ExtensionOverride(baseRule, disable, includeChildren);
        }

        public bool ConflictsWith(ExtensionOverride other) =>
            BaseRule == other.BaseRule &&
            (IncludeSubdirectories != other.IncludeSubdirectories || IsDisable != other.IsDisable);

        public bool IsEqualTo(ExtensionOverride other) =>
            BaseRule == other.BaseRule &&
            IncludeSubdirectories == other.IncludeSubdirectories &&
            IsDisable == other.IsDisable;

        public bool IsChildOf(ExtensionOverride parent)
        {
            if (!parent.IncludeSubdirectories)
            {
                return false;
            }

            return parent.AsRegex().IsMatch(BaseRule);
        }

        public bool MatchesPath(string path) => AsRegex().IsMatch(path);

        public string Output() => $"{(IsDisable ? "!" : string.Empty)}{BaseRule}{(IncludeSubdirectories ? "*" : string.Empty)}";

        private Regex AsRegex()
        {
            var pattern = $"{BaseRule}{(IncludeSubdirectories ? "*" : string.Empty)}";
            var regexString = Regex.Escape(pattern).Replace("/\\*", "(/.*)?");
            return new Regex($"^{regexString}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
