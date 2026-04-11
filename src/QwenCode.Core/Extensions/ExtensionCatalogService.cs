using QwenCode.Core.Compatibility;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Extensions;

/// <summary>
/// Represents the Extension Catalog Service
/// </summary>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="environmentPaths">The environment paths</param>
public sealed partial class ExtensionCatalogService(
    QwenRuntimeProfileService runtimeProfileService,
    IDesktopEnvironmentPaths environmentPaths) : IExtensionCatalogService
{
    private const string ManifestFileName = "qwen-extension.json";
    private const string InstallMetadataFileName = ".qwen-extension-install.json";
    private const string EnablementFileName = "extension-enablement.json";
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting extension snapshot</returns>
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

    /// <summary>
    /// Lists active hooks
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list command hook configuration</returns>
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

    /// <summary>
    /// Gets settings
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension settings snapshot</returns>
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

    /// <summary>
    /// Executes install
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
    public ExtensionSnapshot Install(WorkspacePaths paths, InstallExtensionRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        Directory.CreateDirectory(extensionsDirectory);

        var installContext = PrepareInstallContext(request);
        try
        {
            var sourcePath = installContext.EffectiveSourcePath;

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
                Source = installContext.OriginalSource,
                Type = installContext.MetadataType,
                Ref = installContext.Ref,
                AutoUpdate = request.AutoUpdate,
                AllowPreRelease = request.AllowPreRelease,
                RegistryUrl = string.IsNullOrWhiteSpace(request.RegistryUrl) ? null : request.RegistryUrl
            };
            WriteJson(Path.Combine(wrapperPath, InstallMetadataFileName), JsonSerializer.SerializeToNode(metadata) ?? new JsonObject());

            return Inspect(paths);
        }
        finally
        {
            installContext.Dispose();
        }
    }

    /// <summary>
    /// Executes preview consent
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension consent snapshot</returns>
    public ExtensionConsentSnapshot PreviewConsent(WorkspacePaths paths, InstallExtensionRequest request)
    {
        _ = runtimeProfileService.Inspect(paths);
        var installContext = PrepareInstallContext(request);
        try
        {
            var manifest = LoadManifest(installContext.EffectiveSourcePath);
            var commands = DiscoverCommands(installContext.EffectiveSourcePath, manifest.CommandRoots);
            var skills = DiscoverSkills(installContext.EffectiveSourcePath, manifest.SkillRoots);
            var agents = DiscoverAgents(installContext.EffectiveSourcePath, manifest.AgentRoots);
            var warnings = new List<string>
            {
                "Extensions may introduce unexpected behavior. Review the source before enabling it."
            };

            if (manifest.McpServers.Count > 0)
            {
                warnings.Add("This extension defines MCP servers that will run code or connect to external services.");
            }

            if (manifest.Channels.Count > 0)
            {
                warnings.Add("This extension defines channel integrations that can receive external messages.");
            }

            var summaryLines = new List<string>
            {
                $"Installing extension \"{manifest.Name}\" from {installContext.OriginalSource}"
            };
            if (commands.Count > 0)
            {
                summaryLines.Add($"Commands: {string.Join(", ", commands)}");
            }

            if (skills.Count > 0)
            {
                summaryLines.Add($"Skills: {string.Join(", ", skills)}");
            }

            if (agents.Count > 0)
            {
                summaryLines.Add($"Subagents: {string.Join(", ", agents)}");
            }

            if (manifest.ContextFiles.Count > 0)
            {
                summaryLines.Add($"Context files: {string.Join(", ", manifest.ContextFiles)}");
            }

            return new ExtensionConsentSnapshot
            {
                Name = manifest.Name,
                InstallType = installContext.MetadataType,
                Source = installContext.OriginalSource,
                Summary = string.Join(Environment.NewLine, summaryLines),
                Warnings = warnings,
                Commands = commands,
                Skills = skills,
                Agents = agents,
                McpServers = manifest.McpServers,
                Channels = manifest.Channels,
                ContextFiles = manifest.ContextFiles
            };
        }
        finally
        {
            installContext.Dispose();
        }
    }

    /// <summary>
    /// Creates scaffold
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension scaffold snapshot</returns>
    public ExtensionScaffoldSnapshot CreateScaffold(WorkspacePaths paths, CreateExtensionScaffoldRequest request)
    {
        _ = runtimeProfileService.Inspect(paths);
        var targetPath = Path.GetFullPath(request.TargetPath);
        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            throw new InvalidOperationException($"Path already exists: {targetPath}");
        }

        var extensionName = Path.GetFileName(targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(extensionName))
        {
            throw new InvalidOperationException("Extension target path must end with a directory name.");
        }

        Directory.CreateDirectory(targetPath);
        var createdFiles = new List<string>();
        var template = string.IsNullOrWhiteSpace(request.Template) ? "blank" : request.Template.Trim().ToLowerInvariant();

        var manifestObject = new JsonObject
        {
            ["name"] = extensionName,
            ["version"] = "1.0.0",
            ["description"] = $"{extensionName} extension"
        };

        switch (template)
        {
            case "commands":
                Directory.CreateDirectory(Path.Combine(targetPath, "commands", "example"));
                File.WriteAllText(Path.Combine(targetPath, "commands", "example", "hello.md"), "# Hello command");
                createdFiles.Add(Path.Combine(targetPath, "commands", "example", "hello.md"));
                manifestObject["commands"] = new JsonArray("commands");
                break;
            case "skills":
                Directory.CreateDirectory(Path.Combine(targetPath, "skills", "example-skill"));
                File.WriteAllText(Path.Combine(targetPath, "skills", "example-skill", "SKILL.md"), "---\nname: example-skill\n---\nDescribe the skill here\n");
                createdFiles.Add(Path.Combine(targetPath, "skills", "example-skill", "SKILL.md"));
                manifestObject["skills"] = new JsonArray("skills");
                break;
            case "agent":
            case "agents":
                Directory.CreateDirectory(Path.Combine(targetPath, "agents", "example-agent"));
                File.WriteAllText(Path.Combine(targetPath, "agents", "example-agent", "AGENT.md"), "# Example agent");
                createdFiles.Add(Path.Combine(targetPath, "agents", "example-agent", "AGENT.md"));
                manifestObject["agents"] = new JsonArray("agents");
                break;
            case "context":
                File.WriteAllText(Path.Combine(targetPath, "QWEN.md"), "# Project context");
                createdFiles.Add(Path.Combine(targetPath, "QWEN.md"));
                manifestObject["contextFileName"] = new JsonArray("QWEN.md");
                break;
            case "mcp-server":
            case "mcp":
                File.WriteAllText(Path.Combine(targetPath, "server.js"), "console.log('extension mcp server');\n");
                createdFiles.Add(Path.Combine(targetPath, "server.js"));
                manifestObject["mcpServers"] = new JsonObject
                {
                    ["example"] = new JsonObject
                    {
                        ["command"] = "node",
                        ["args"] = new JsonArray("server.js")
                    }
                };
                break;
            case "blank":
                break;
            default:
                throw new InvalidOperationException($"Unsupported extension scaffold template: {request.Template}");
        }

        var manifestPath = Path.Combine(targetPath, ManifestFileName);
        File.WriteAllText(manifestPath, manifestObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        createdFiles.Insert(0, manifestPath);

        return new ExtensionScaffoldSnapshot
        {
            Name = extensionName,
            Path = targetPath,
            Template = template,
            CreatedManifest = true,
            CreatedFiles = createdFiles
        };
    }

    /// <summary>
    /// Updates value
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
    public ExtensionSnapshot Update(WorkspacePaths paths, UpdateExtensionRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var extensionsDirectory = GetExtensionsDirectory(runtimeProfile);
        if (!Directory.Exists(extensionsDirectory))
        {
            return Inspect(paths);
        }

        var wrappers = request.UpdateAll
            ? Directory.EnumerateDirectories(extensionsDirectory).ToArray()
            : [FindExtensionWrapperPath(extensionsDirectory, request.Name)
               ?? throw new InvalidOperationException($"Extension \"{request.Name}\" was not found.")];

        foreach (var wrapperPath in wrappers)
        {
            var metadata = ReadInstallMetadata(wrapperPath)
                           ?? throw new InvalidOperationException($"Extension at \"{wrapperPath}\" has no install metadata and cannot be updated.");
            var effectivePath = ResolveEffectivePath(wrapperPath, metadata);
            var previousManifest = LoadManifest(effectivePath);

            var installRequest = new InstallExtensionRequest
            {
                SourcePath = metadata.Source,
                InstallMode = string.Equals(metadata.Type, "link", StringComparison.OrdinalIgnoreCase) ? "link" : "copy",
                SourceType = metadata.Type,
                Ref = metadata.Ref ?? string.Empty,
                AutoUpdate = metadata.AutoUpdate ?? false,
                AllowPreRelease = metadata.AllowPreRelease ?? false,
                RegistryUrl = metadata.RegistryUrl ?? string.Empty
            };

            Install(paths, installRequest);

            if (metadata.AutoUpdate == true)
            {
                var refreshedWrapperPath = FindExtensionWrapperPath(extensionsDirectory, previousManifest.Name) ?? wrapperPath;
                var refreshedMetadata = ReadInstallMetadata(refreshedWrapperPath);
                if (refreshedMetadata is not null)
                {
                    WriteJson(
                        Path.Combine(refreshedWrapperPath, InstallMetadataFileName),
                        JsonSerializer.SerializeToNode(new ExtensionInstallMetadata
                        {
                            Source = refreshedMetadata.Source,
                            Type = refreshedMetadata.Type,
                            Ref = refreshedMetadata.Ref,
                            AutoUpdate = true,
                            AllowPreRelease = refreshedMetadata.AllowPreRelease,
                            RegistryUrl = refreshedMetadata.RegistryUrl,
                            ReleaseTag = refreshedMetadata.ReleaseTag
                        }) ?? new JsonObject());
                }
            }
        }

        return Inspect(paths);
    }

    /// <summary>
    /// Sets setting
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension settings snapshot</returns>
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

    /// <summary>
    /// Sets enabled
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
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

    /// <summary>
    /// Removes value
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting extension snapshot</returns>
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

    private static PreparedInstallContext PrepareInstallContext(InstallExtensionRequest request)
    {
        var normalizedType = string.IsNullOrWhiteSpace(request.SourceType)
            ? "local"
            : request.SourceType.Trim().ToLowerInvariant();
        var normalizedMode = request.InstallMode.Trim().ToLowerInvariant();
        var sourcePath = Path.GetFullPath(request.SourcePath);

        return normalizedType switch
        {
            "local" => PrepareLocalInstallContext(sourcePath, normalizedMode, request),
            "link" => PrepareLocalInstallContext(sourcePath, "link", request),
            "git" => PrepareGitInstallContext(sourcePath, request),
            _ => throw new InvalidOperationException($"Unsupported extension source type: {request.SourceType}")
        };
    }

    private static PreparedInstallContext PrepareLocalInstallContext(
        string sourcePath,
        string installMode,
        InstallExtensionRequest request)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Extension source directory was not found: {sourcePath}");
        }

        return new PreparedInstallContext(
            sourcePath,
            sourcePath,
            string.Equals(installMode, "link", StringComparison.OrdinalIgnoreCase) ? "link" : "local",
            request.Ref);
    }

    private static PreparedInstallContext PrepareGitInstallContext(string source, InstallExtensionRequest request)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"qwen-extension-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            RunGit("clone --depth 1 " + QuoteArgument(source) + " " + QuoteArgument(tempDirectory), Environment.CurrentDirectory);
            if (!string.IsNullOrWhiteSpace(request.Ref))
            {
                RunGit("checkout " + QuoteArgument(request.Ref), tempDirectory);
            }

            return new PreparedInstallContext(tempDirectory, source, "git", request.Ref, tempDirectory);
        }
        catch
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }

            throw;
        }
    }

    private static void RunGit(string arguments, string workingDirectory)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start git process.");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {process.ExitCode}: {stderr}{Environment.NewLine}{stdout}".Trim());
        }
    }

    private static string QuoteArgument(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

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
        /// <summary>
        /// Gets or sets the overrides
        /// </summary>
        public List<string> Overrides { get; set; } = [];
    }

    private sealed class PreparedInstallContext(
        string effectiveSourcePath,
        string originalSource,
        string metadataType,
        string? @ref,
        string? temporaryDirectory = null) : IDisposable
    {
        /// <summary>
        /// Gets the effective source path
        /// </summary>
        public string EffectiveSourcePath { get; } = effectiveSourcePath;

        /// <summary>
        /// Gets the original source
        /// </summary>
        public string OriginalSource { get; } = originalSource;

        /// <summary>
        /// Gets the metadata type
        /// </summary>
        public string MetadataType { get; } = metadataType;

        /// <summary>
        /// Gets the ref
        /// </summary>
        public string? Ref { get; } = @ref;

        /// <summary>
        /// Gets the temporary directory
        /// </summary>
        public string? TemporaryDirectory { get; } = temporaryDirectory;

        /// <summary>
        /// Executes dispose
        /// </summary>
        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(TemporaryDirectory) && Directory.Exists(TemporaryDirectory))
            {
                Directory.Delete(TemporaryDirectory, recursive: true);
            }
        }
    }

    private sealed class ExtensionOverride(string baseRule, bool isDisable, bool includeSubdirectories)
    {
        /// <summary>
        /// Gets the base rule
        /// </summary>
        public string BaseRule { get; } = baseRule;

        /// <summary>
        /// Gets a value indicating whether is disable
        /// </summary>
        public bool IsDisable { get; } = isDisable;

        /// <summary>
        /// Gets the include subdirectories
        /// </summary>
        public bool IncludeSubdirectories { get; } = includeSubdirectories;

        /// <summary>
        /// Executes from input
        /// </summary>
        /// <param name="inputRule">The input rule</param>
        /// <param name="includeSubdirectories">The include subdirectories</param>
        /// <returns>The resulting extension override</returns>
        public static ExtensionOverride FromInput(string inputRule, bool includeSubdirectories)
        {
            var disable = inputRule.StartsWith('!');
            var baseRule = disable ? inputRule[1..] : inputRule;
            return new ExtensionOverride(EnsureLeadingAndTrailingSlash(baseRule), disable, includeSubdirectories);
        }

        /// <summary>
        /// Executes from file rule
        /// </summary>
        /// <param name="fileRule">The file rule</param>
        /// <returns>The resulting extension override</returns>
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

        /// <summary>
        /// Executes conflicts with
        /// </summary>
        /// <param name="other">The other</param>
        /// <returns>A value indicating whether the operation succeeded</returns>
        public bool ConflictsWith(ExtensionOverride other) =>
            BaseRule == other.BaseRule &&
            (IncludeSubdirectories != other.IncludeSubdirectories || IsDisable != other.IsDisable);

        /// <summary>
        /// Executes is equal to
        /// </summary>
        /// <param name="other">The other</param>
        /// <returns>A value indicating whether the operation succeeded</returns>
        public bool IsEqualTo(ExtensionOverride other) =>
            BaseRule == other.BaseRule &&
            IncludeSubdirectories == other.IncludeSubdirectories &&
            IsDisable == other.IsDisable;

        /// <summary>
        /// Executes is child of
        /// </summary>
        /// <param name="parent">The parent</param>
        /// <returns>A value indicating whether the operation succeeded</returns>
        public bool IsChildOf(ExtensionOverride parent)
        {
            if (!parent.IncludeSubdirectories)
            {
                return false;
            }

            return parent.AsRegex().IsMatch(BaseRule);
        }

        /// <summary>
        /// Executes matches path
        /// </summary>
        /// <param name="path">The path to process</param>
        /// <returns>A value indicating whether the operation succeeded</returns>
        public bool MatchesPath(string path) => AsRegex().IsMatch(path);

        /// <summary>
        /// Executes output
        /// </summary>
        /// <returns>The resulting string</returns>
        public string Output() => $"{(IsDisable ? "!" : string.Empty)}{BaseRule}{(IncludeSubdirectories ? "*" : string.Empty)}";

        private Regex AsRegex()
        {
            var pattern = $"{BaseRule}{(IncludeSubdirectories ? "*" : string.Empty)}";
            var regexString = Regex.Escape(pattern).Replace("/\\*", "(/.*)?");
            return new Regex($"^{regexString}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
