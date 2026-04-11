using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;

namespace QwenCode.Core.Mcp;

/// <summary>
/// Represents the Mcp Registry Service
/// </summary>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="tokenStore">The token store</param>
public sealed class McpRegistryService(
    QwenRuntimeProfileService runtimeProfileService,
    IMcpTokenStore tokenStore) : IMcpRegistry
{
    /// <summary>
    /// Lists servers
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list mcp server definition</returns>
    public IReadOnlyList<McpServerDefinition> ListServers(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var userSettingsPath = Path.Combine(runtimeProfile.GlobalQwenDirectory, "settings.json");
        var projectSettingsPath = Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "settings.json");

        var servers = new Dictionary<string, McpServerDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ReadServers(userSettingsPath, "user"))
        {
            servers[item.Name] = item;
        }

        foreach (var item in ReadServers(projectSettingsPath, "project"))
        {
            servers[item.Name] = item;
        }

        return servers.Values
            .OrderBy(static item => item.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Executes add server
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting mcp server definition</returns>
    public McpServerDefinition AddServer(WorkspacePaths paths, McpServerRegistrationRequest request)
    {
        ValidateRequest(request);

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var settingsPath = ResolveSettingsPath(runtimeProfile, request.Scope);
        var root = LoadSettingsRoot(settingsPath);
        var mcpServers = root["mcpServers"] as JsonObject ?? [];
        root["mcpServers"] = mcpServers;
        mcpServers[request.Name] = BuildServerObject(request);

        SaveSettingsRoot(settingsPath, root);

        return ReadServers(settingsPath, NormalizeScope(request.Scope))
            .First(item => string.Equals(item.Name, request.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Removes server
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <param name="scope">The scope</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool RemoveServer(WorkspacePaths paths, string name, string scope)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var settingsPath = ResolveSettingsPath(runtimeProfile, scope);
        var root = LoadSettingsRoot(settingsPath);
        if (root["mcpServers"] is not JsonObject mcpServers || !mcpServers.Remove(name))
        {
            return false;
        }

        if (mcpServers.Count == 0)
        {
            root.Remove("mcpServers");
        }

        SaveSettingsRoot(settingsPath, root);
        tokenStore.DeleteTokenAsync(name).GetAwaiter().GetResult();
        return true;
    }

    private IReadOnlyList<McpServerDefinition> ReadServers(string settingsPath, string scope)
    {
        if (!File.Exists(settingsPath))
        {
            return [];
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject;
            if (root?["mcpServers"] is not JsonObject mcpServers)
            {
                return [];
            }

            return mcpServers
                .Select(pair => TryParseServer(pair.Key, pair.Value, scope, settingsPath))
                .OfType<McpServerDefinition>()
                .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private McpServerDefinition? TryParseServer(string name, JsonNode? node, string scope, string settingsPath)
    {
        if (node is not JsonObject server)
        {
            return null;
        }

        var transport =
            server["httpUrl"]?.GetValue<string?>() is { Length: > 0 } ? "http" :
            server["url"]?.GetValue<string?>() is { Length: > 0 } ? "sse" :
            "stdio";
        var commandOrUrl =
            server["httpUrl"]?.GetValue<string?>() ??
            server["url"]?.GetValue<string?>() ??
            server["command"]?.GetValue<string?>() ??
            string.Empty;

        var headers = ReadStringDictionary(server["headers"]);
        var hasPersistedToken = tokenStore.HasToken(name);
        var hasStaticAuthorizationHeader = headers.ContainsKey("Authorization");

        return new McpServerDefinition
        {
            Name = name,
            Scope = scope,
            Transport = transport,
            CommandOrUrl = commandOrUrl,
            Arguments = ReadStringArray(server["args"]),
            EnvironmentVariables = ReadStringDictionary(server["env"]),
            Headers = headers,
            TimeoutMs = server["timeout"]?.GetValue<int?>(),
            Trust = server["trust"]?.GetValue<bool?>() ?? false,
            Description = server["description"]?.GetValue<string?>() ?? string.Empty,
            Instructions = server["instructions"]?.GetValue<string?>() ?? string.Empty,
            IncludeTools = ReadStringArray(server["includeTools"]),
            ExcludeTools = ReadStringArray(server["excludeTools"]),
            SettingsPath = settingsPath,
            HasPersistedToken = hasPersistedToken,
            HasStaticAuthorizationHeader = hasStaticAuthorizationHeader,
            AuthenticationStatus = BuildAuthenticationStatus(hasPersistedToken, hasStaticAuthorizationHeader)
        };
    }

    private static string BuildAuthenticationStatus(bool hasPersistedToken, bool hasStaticAuthorizationHeader) =>
        hasPersistedToken ? "persisted-token" :
        hasStaticAuthorizationHeader ? "static-header" :
        "none";

    private static JsonObject BuildServerObject(McpServerRegistrationRequest request)
    {
        var server = new JsonObject
        {
            ["timeout"] = request.TimeoutMs,
            ["trust"] = request.Trust,
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description,
            ["instructions"] = string.IsNullOrWhiteSpace(request.Instructions) ? null : request.Instructions
        };

        switch (NormalizeTransport(request.Transport))
        {
            case "http":
                server["httpUrl"] = request.CommandOrUrl;
                break;
            case "sse":
                server["url"] = request.CommandOrUrl;
                break;
            default:
                server["command"] = request.CommandOrUrl;
                server["args"] = new JsonArray(request.Arguments.Select(static argument => JsonValue.Create(argument)).ToArray());
                if (request.EnvironmentVariables.Count > 0)
                {
                    server["env"] = CreateStringDictionaryObject(request.EnvironmentVariables);
                }
                break;
        }

        if (request.Headers.Count > 0)
        {
            server["headers"] = CreateStringDictionaryObject(request.Headers);
        }

        if (request.IncludeTools.Count > 0)
        {
            server["includeTools"] = new JsonArray(request.IncludeTools.Select(static tool => JsonValue.Create(tool)).ToArray());
        }

        if (request.ExcludeTools.Count > 0)
        {
            server["excludeTools"] = new JsonArray(request.ExcludeTools.Select(static tool => JsonValue.Create(tool)).ToArray());
        }

        return server;
    }

    private static JsonObject CreateStringDictionaryObject(IReadOnlyDictionary<string, string> values)
    {
        var result = new JsonObject();
        foreach (var pair in values)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> ReadStringDictionary(JsonNode? node)
    {
        if (node is not JsonObject jsonObject)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return jsonObject
            .Where(static pair => pair.Value is JsonValue)
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value?.GetValue<string>() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return [];
        }

        return array
            .Select(static item => item?.GetValue<string>())
            .OfType<string>()
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static JsonObject LoadSettingsRoot(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            return [];
        }

        return JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? [];
    }

    private static void SaveSettingsRoot(string settingsPath, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(
            settingsPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ResolveSettingsPath(QwenRuntimeProfile runtimeProfile, string scope) =>
        NormalizeScope(scope) switch
        {
            "project" => Path.Combine(runtimeProfile.ProjectRoot, ".qwen", "settings.json"),
            _ => Path.Combine(runtimeProfile.GlobalQwenDirectory, "settings.json")
        };

    private static void ValidateRequest(McpServerRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("MCP server name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CommandOrUrl))
        {
            throw new InvalidOperationException("MCP server command or URL is required.");
        }

        var transport = NormalizeTransport(request.Transport);
        if (transport is "http" or "sse" &&
            !Uri.TryCreate(request.CommandOrUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("HTTP and SSE transports require an absolute URL.");
        }
    }

    private static string NormalizeScope(string scope) =>
        string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase) ? "project" : "user";

    private static string NormalizeTransport(string transport) => transport.ToLowerInvariant() switch
    {
        "http" => "http",
        "sse" => "sse",
        _ => "stdio"
    };
}
