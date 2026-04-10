using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.Core.Extensions;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Hooks;

/// <summary>
/// Represents the Hook Registry Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="extensionCatalogService">The extension catalog service</param>
public sealed class HookRegistryService(
    IDesktopEnvironmentPaths environmentPaths,
    IExtensionCatalogService? extensionCatalogService = null)
{
    /// <summary>
    /// Builds user prompt submit plan
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <returns>The resulting hook execution plan</returns>
    public HookExecutionPlan BuildUserPromptSubmitPlan(QwenRuntimeProfile runtimeProfile)
        => BuildPlan(
            runtimeProfile,
            new HookInvocationRequest
            {
                EventName = HookEventName.UserPromptSubmit,
                SessionId = string.Empty,
                WorkingDirectory = runtimeProfile.ProjectRoot,
                TranscriptPath = string.Empty
            });

    /// <summary>
    /// Builds plan
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="eventName">The event name</param>
    /// <returns>The resulting hook execution plan</returns>
    public HookExecutionPlan BuildPlan(QwenRuntimeProfile runtimeProfile, HookEventName eventName)
        => BuildPlan(
            runtimeProfile,
            new HookInvocationRequest
            {
                EventName = eventName,
                SessionId = string.Empty,
                WorkingDirectory = runtimeProfile.ProjectRoot,
                TranscriptPath = string.Empty
            });

    /// <summary>
    /// Builds plan
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting hook execution plan</returns>
    public HookExecutionPlan BuildPlan(QwenRuntimeProfile runtimeProfile, HookInvocationRequest request)
    {
        var eventName = request.EventName;
        var projectSettingsPath = Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "settings.json");
        var userSettingsPath = Path.Combine(runtimeProfile.GlobalQwenDirectory, "settings.json");
        var systemRoot = ResolveProgramDataRoot();
        var systemDefaultsPath = GetSystemDefaultsPath(systemRoot);
        var systemSettingsPath = GetSystemSettingsPath(systemRoot);

        var state = MergeHookState(
            runtimeProfile,
            [
                systemDefaultsPath,
                userSettingsPath,
                projectSettingsPath,
                systemSettingsPath
            ]);

        if (!state.Enabled)
        {
            return new HookExecutionPlan
            {
                Enabled = false
            };
        }

        var hooks = new List<CommandHookConfiguration>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in EnumerateHookSources(runtimeProfile, systemDefaultsPath, userSettingsPath, projectSettingsPath, systemSettingsPath))
        {
            if (!File.Exists(layer.Path))
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(layer.Path);
                using var document = JsonDocument.Parse(stream);
                if (!TryNavigate(document.RootElement, ["hooks", eventName.ToString()], out var definitions) ||
                    definitions.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var definition in definitions.EnumerateArray())
                {
                    if (definition.ValueKind != JsonValueKind.Object ||
                        !definition.TryGetProperty("hooks", out var hooksElement) ||
                        hooksElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var sequential = TryGetBoolean(definition, "sequential");
                    var matcher = TryGetString(definition, "matcher", out var parsedMatcher) ? parsedMatcher : string.Empty;
                    foreach (var hookElement in hooksElement.EnumerateArray())
                    {
                        if (!TryParseHook(hookElement, layer.Source, eventName, sequential, matcher, out var hook))
                        {
                            continue;
                        }

                        if (!MatchesRequest(hook, request))
                        {
                            continue;
                        }

                        var hookKey = BuildHookKey(hook);
                        if (state.Disabled.Contains(hook.Name) ||
                            state.Disabled.Contains(hook.Command) ||
                            !seenKeys.Add(hookKey))
                        {
                            continue;
                        }

                        hooks.Add(hook);
                    }
                }
            }
            catch
            {
                // Ignore malformed hook configuration layers.
            }
        }

        if (extensionCatalogService is not null)
        {
            foreach (var hook in extensionCatalogService.ListActiveHooks(new WorkspacePaths
                     {
                         WorkspaceRoot = runtimeProfile.ProjectRoot
                     }))
            {
                if (!MatchesRequest(hook, request))
                {
                    continue;
                }

                var hookKey = BuildHookKey(hook);
                if (state.Disabled.Contains(hook.Name) ||
                    state.Disabled.Contains(hook.Command) ||
                    !seenKeys.Add(hookKey))
                {
                    continue;
                }

                hooks.Add(hook);
            }
        }

        return new HookExecutionPlan
        {
            Enabled = true,
            Sequential = hooks.Any(static hook => hook.Sequential),
            Hooks = hooks
        };
    }

    private (bool Enabled, HashSet<string> Disabled) MergeHookState(
        QwenRuntimeProfile runtimeProfile,
        IReadOnlyList<string> settingsPaths)
    {
        var enabled = true;
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in settingsPaths.Where(File.Exists))
        {
            if (IsProjectSettings(path, runtimeProfile) && !runtimeProfile.IsWorkspaceTrusted)
            {
                continue;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var document = JsonDocument.Parse(stream);
                if (TryNavigate(document.RootElement, ["hooksConfig", "enabled"], out var enabledElement) &&
                    enabledElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    enabled = enabledElement.GetBoolean();
                }

                if (TryNavigate(document.RootElement, ["hooksConfig", "disabled"], out var disabledElement) &&
                    disabledElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in disabledElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String &&
                            item.GetString() is { Length: > 0 } value)
                        {
                            disabled.Add(value);
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed hook configuration layers.
            }
        }

        return (enabled, disabled);
    }

    private IEnumerable<(string Path, HookConfigSource Source)> EnumerateHookSources(
        QwenRuntimeProfile runtimeProfile,
        string systemDefaultsPath,
        string userSettingsPath,
        string projectSettingsPath,
        string systemSettingsPath)
    {
        if (runtimeProfile.IsWorkspaceTrusted)
        {
            yield return (projectSettingsPath, HookConfigSource.Project);
        }

        yield return (userSettingsPath, HookConfigSource.User);
        yield return (systemSettingsPath, HookConfigSource.System);
        yield return (systemDefaultsPath, HookConfigSource.System);
    }

    private static bool TryParseHook(
        JsonElement hookElement,
        HookConfigSource source,
        HookEventName eventName,
        bool sequential,
        string matcher,
        out CommandHookConfiguration hook)
    {
        hook = null!;
        if (hookElement.ValueKind != JsonValueKind.Object ||
            !TryGetString(hookElement, "type", out var type) ||
            !string.Equals(type, "command", StringComparison.OrdinalIgnoreCase) ||
            !TryGetString(hookElement, "command", out var command))
        {
            return false;
        }

        hook = new CommandHookConfiguration
        {
            Command = command,
            Name = TryGetString(hookElement, "name", out var name) ? name : string.Empty,
            Matcher = matcher,
            Description = TryGetString(hookElement, "description", out var description) ? description : string.Empty,
            TimeoutMs = TryGetInt32(hookElement, "timeout", out var timeoutMs) && timeoutMs > 0 ? timeoutMs : 60_000,
            EnvironmentVariables = ReadEnvironmentVariables(hookElement),
            Source = source,
            EventName = eventName,
            Sequential = sequential
        };
        return true;
    }

    private static IReadOnlyDictionary<string, string> ReadEnvironmentVariables(JsonElement hookElement)
    {
        if (!hookElement.TryGetProperty("env", out var envElement) || envElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return envElement.EnumerateObject()
            .Where(static property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(
                static property => property.Name,
                static property => property.Value.GetString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
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

    private static bool IsProjectSettings(string path, QwenRuntimeProfile runtimeProfile) =>
        Path.GetFullPath(path).StartsWith(
            Path.GetFullPath(runtimeProfile.ProjectRoot),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string BuildHookKey(CommandHookConfiguration hook) =>
        string.IsNullOrWhiteSpace(hook.Name)
            ? hook.Command
            : $"{hook.Name}:{hook.Command}:{hook.Matcher}";

    private static bool MatchesRequest(CommandHookConfiguration hook, HookInvocationRequest request)
    {
        if (string.IsNullOrWhiteSpace(hook.Matcher) || hook.Matcher == "*")
        {
            return true;
        }

        return request.EventName switch
        {
            HookEventName.PreToolUse or HookEventName.PostToolUse or HookEventName.PostToolUseFailure or HookEventName.PermissionRequest
                => MatchesRegexOrExact(hook.Matcher, request.ToolName),
            HookEventName.SubagentStart or HookEventName.SubagentStop
                => MatchesRegexOrExact(hook.Matcher, request.AgentName),
            HookEventName.PreCompact
                => string.Equals(hook.Matcher, GetMetadataString(request, "trigger"), StringComparison.Ordinal),
            HookEventName.Notification
                => string.Equals(hook.Matcher, GetMetadataString(request, "notification_type"), StringComparison.Ordinal),
            HookEventName.SessionStart or HookEventName.SessionEnd
                => MatchesRegexOrExact(hook.Matcher, GetMetadataString(request, "trigger")),
            _ => true
        };
    }

    private static bool MatchesRegexOrExact(string matcher, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return true;
        }

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(candidate, matcher);
        }
        catch
        {
            return string.Equals(matcher, candidate, StringComparison.Ordinal);
        }
    }

    private static string GetMetadataString(HookInvocationRequest request, string propertyName) =>
        request.Metadata.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var stringValue)
            ? stringValue
            : string.Empty;

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

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = default;
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    private static bool TryGetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        property.GetBoolean();
}
