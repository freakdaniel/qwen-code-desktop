using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;

namespace QwenCode.Core.Mcp;

/// <summary>
/// Represents the Mcp Tool Runtime Service
/// </summary>
/// <param name="registry">The registry</param>
/// <param name="tokenStore">The token store</param>
/// <param name="httpClient">The http client</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
public sealed class McpToolRuntimeService(
    IMcpRegistry registry,
    IMcpTokenStore tokenStore,
    HttpClient httpClient,
    QwenRuntimeProfileService runtimeProfileService) : IMcpToolRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly ConcurrentDictionary<string, PooledSession> sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Connects server async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp reconnect result</returns>
    public async Task<McpReconnectResult> ConnectServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP tools and prompts are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: true, cancellationToken);
        var tools = await session.ListToolsAsync(cancellationToken);
        var prompts = await session.ListPromptsAsync(cancellationToken);
        var resources = await session.ListResourcesAsync(cancellationToken);
        return new McpReconnectResult
        {
            Name = server.Name,
            Status = "connected",
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            Message = BuildConnectedMessage(server.Name, tools.Count, prompts.Count, resources.Count),
            DiscoveredToolsCount = tools.Count,
            DiscoveredPromptsCount = prompts.Count,
            DiscoveredResourcesCount = resources.Count,
            SupportsPrompts = session.SupportsPrompts,
            SupportsResources = session.SupportsResources,
            LastDiscoveryUtc = session.LastDiscoveryUtc
        };
    }

    /// <summary>
    /// Executes probe server async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp reconnect result</returns>
    public async Task<McpReconnectResult> ProbeServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP tools and prompts are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: false, cancellationToken);
        var tools = await session.ListToolsAsync(cancellationToken);
        var prompts = await session.ListPromptsAsync(cancellationToken);
        var resources = await session.ListResourcesAsync(cancellationToken);
        return new McpReconnectResult
        {
            Name = server.Name,
            Status = "connected",
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            Message = $"MCP server '{server.Name}' is healthy.",
            DiscoveredToolsCount = tools.Count,
            DiscoveredPromptsCount = prompts.Count,
            DiscoveredResourcesCount = resources.Count,
            SupportsPrompts = session.SupportsPrompts,
            SupportsResources = session.SupportsResources,
            LastDiscoveryUtc = session.LastDiscoveryUtc
        };
    }

    /// <summary>
    /// Disconnects server async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DisconnectServerAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default)
    {
        var key = BuildSessionKey(paths, serverName);
        if (!sessions.TryRemove(key, out var session))
        {
            return;
        }

        await session.DisposeAsync();
    }

    /// <summary>
    /// Executes describe async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    public async Task<string> DescribeAsync(
        WorkspacePaths paths,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP tools and prompts are unavailable in untrusted folders.");
        var configuredServers = registry.ListServers(paths);
        if (configuredServers.Count == 0)
        {
            return "No MCP servers are configured in qwen-compatible settings.";
        }

        var requestedServer = TryGetString(arguments, "server_name");
        var includeSchema = TryGetBoolean(arguments, "include_schema") ?? false;
        var servers = string.IsNullOrWhiteSpace(requestedServer)
            ? configuredServers
            : configuredServers
                .Where(server => string.Equals(server.Name, requestedServer, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (servers.Count == 0)
        {
            throw new InvalidOperationException($"MCP server '{requestedServer}' is not configured.");
        }

        var sections = new List<string>();
        foreach (var server in servers)
        {
            try
            {
                var tools = await ListToolsForServerAsync(paths, server, cancellationToken);
                var prompts = await ListPromptsForServerAsync(paths, server, cancellationToken);
                var resources = await ListResourcesForServerAsync(paths, server, cancellationToken);
                sections.Add(FormatServerSummary(server, tools, prompts, resources, includeSchema));
            }
            catch (Exception exception)
            {
                sections.Add(
                    string.Join(
                        Environment.NewLine,
                        FormatServerHeader(server),
                        $"Discovery failed: {exception.Message}"));
            }
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    /// <summary>
    /// Lists prompts async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to i read only list mcp prompt definition</returns>
    public async Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP prompts are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        return await ListPromptsForServerAsync(paths, server, cancellationToken);
    }

    /// <summary>
    /// Lists resources async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to i read only list mcp resource definition</returns>
    public async Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(
        WorkspacePaths paths,
        string serverName,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP resources are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        return await ListResourcesForServerAsync(paths, server, cancellationToken);
    }

    /// <summary>
    /// Resolves tool async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp tool definition</returns>
    public async Task<McpToolDefinition> ResolveToolAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP tools are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        var tools = await ListToolsForServerAsync(paths, server, cancellationToken);
        var resolved = tools.FirstOrDefault(item => string.Equals(item.Name, toolName, StringComparison.OrdinalIgnoreCase));
        if (resolved is null)
        {
            throw new InvalidOperationException($"MCP tool '{toolName}' was not found on server '{serverName}'.");
        }

        return resolved;
    }

    /// <summary>
    /// Invokes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="toolName">The tool name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp tool invocation result</returns>
    public async Task<McpToolInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP tools are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        _ = await ResolveToolAsync(paths, serverName, toolName, cancellationToken);

        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: false, cancellationToken);
        var result = await session.CallToolAsync(toolName, ExtractToolArguments(arguments), cancellationToken);

        return new McpToolInvocationResult
        {
            ServerName = serverName,
            ToolName = toolName,
            Output = FormatCallResult(result),
            IsError = result.IsError
        };
    }

    /// <summary>
    /// Reads resource async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="uri">The uri</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp resource read result</returns>
    public async Task<McpResourceReadResult> ReadResourceAsync(
        WorkspacePaths paths,
        string serverName,
        string uri,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP resources are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        if (!server.Trust)
        {
            throw new InvalidOperationException("MCP resources are unavailable on untrusted servers.");
        }

        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: false, cancellationToken);
        var result = await session.ReadResourceAsync(uri, cancellationToken);

        return new McpResourceReadResult
        {
            ServerName = serverName,
            Uri = uri,
            Output = FormatResourceResult(result)
        };
    }

    /// <summary>
    /// Gets prompt async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="serverName">The server name</param>
    /// <param name="promptName">The prompt name</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    public async Task<McpPromptInvocationResult> GetPromptAsync(
        WorkspacePaths paths,
        string serverName,
        string promptName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        EnsureWorkspaceTrust(paths, "MCP prompts are unavailable in untrusted folders.");
        var server = ResolveServer(paths, serverName);
        var prompts = await ListPromptsForServerAsync(paths, server, cancellationToken);
        if (!prompts.Any(item => string.Equals(item.Name, promptName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"MCP prompt '{promptName}' was not found on server '{serverName}'.");
        }

        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: false, cancellationToken);
        var result = await session.GetPromptAsync(promptName, ExtractToolArguments(arguments), cancellationToken);

        return new McpPromptInvocationResult
        {
            ServerName = serverName,
            PromptName = promptName,
            Output = FormatPromptResult(result)
        };
    }

    private McpServerDefinition ResolveServer(WorkspacePaths paths, string serverName)
    {
        var server = registry.ListServers(paths)
            .FirstOrDefault(item => string.Equals(item.Name, serverName, StringComparison.OrdinalIgnoreCase));
        if (server is null)
        {
            throw new InvalidOperationException($"MCP server '{serverName}' is not configured.");
        }

        return server;
    }

    private async Task<IReadOnlyList<McpToolDefinition>> ListToolsForServerAsync(
        WorkspacePaths paths,
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: false, cancellationToken);
        var tools = await session.ListToolsAsync(cancellationToken);

        return tools
            .Where(tool => server.IncludeTools.Count == 0 || server.IncludeTools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
            .Where(tool => !server.ExcludeTools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(static tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<McpPromptDefinition>> ListPromptsForServerAsync(
        WorkspacePaths paths,
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: false, cancellationToken);
        var prompts = await session.ListPromptsAsync(cancellationToken);
        return prompts
            .OrderBy(static prompt => prompt.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<McpResourceDefinition>> ListResourcesForServerAsync(
        WorkspacePaths paths,
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        var session = await GetOrCreateSessionAsync(paths, server, forceReconnect: false, cancellationToken);
        var resources = await session.ListResourcesAsync(cancellationToken);
        return resources
            .OrderBy(static resource => resource.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static resource => resource.Uri, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<PooledSession> GetOrCreateSessionAsync(
        WorkspacePaths paths,
        McpServerDefinition server,
        bool forceReconnect,
        CancellationToken cancellationToken)
    {
        var key = BuildSessionKey(paths, server.Name);
        if (forceReconnect && sessions.TryRemove(key, out var existing))
        {
            await existing.DisposeAsync();
        }

        var session = sessions.GetOrAdd(key, _ => new PooledSession(server));
        try
        {
            await session.EnsureConnectedAsync(
                () => McpProtocolSession.ConnectAsync(server, httpClient, tokenStore, cancellationToken),
                cancellationToken);
            return session;
        }
        catch
        {
            sessions.TryRemove(key, out _);
            await session.DisposeAsync();
            throw;
        }
    }

    private static string BuildSessionKey(WorkspacePaths paths, string serverName) =>
        $"{Path.GetFullPath(paths.WorkspaceRoot ?? Environment.CurrentDirectory)}::{serverName}";

    private void EnsureWorkspaceTrust(WorkspacePaths paths, string message)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        if (!runtimeProfile.IsWorkspaceTrusted)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string FormatServerSummary(
        McpServerDefinition server,
        IReadOnlyList<McpToolDefinition> tools,
        IReadOnlyList<McpPromptDefinition> prompts,
        IReadOnlyList<McpResourceDefinition> resources,
        bool includeSchema)
    {
        var lines = new List<string> { FormatServerHeader(server) };

        if (!string.IsNullOrWhiteSpace(server.Description))
        {
            lines.Add(server.Description);
        }

        if (prompts.Count == 0 && resources.Count == 0 && tools.Count == 0)
        {
            lines.Add("No tools, prompts, or resources discovered.");
            return string.Join(Environment.NewLine, lines);
        }

        if (prompts.Count > 0)
        {
            lines.Add("Prompts:");
            foreach (var prompt in prompts)
            {
                lines.Add($"- {server.Name}/{prompt.Name}");
                if (!string.IsNullOrWhiteSpace(prompt.Description))
                {
                    lines.Add($"  {prompt.Description}");
                }

                if (includeSchema)
                {
                    lines.Add($"  arguments: {prompt.ArgumentsJson}");
                }
            }
        }

        if (resources.Count > 0)
        {
            lines.Add("Resources:");
            foreach (var resource in resources)
            {
                lines.Add($"- {server.Name}/{resource.Uri}");
                if (!string.IsNullOrWhiteSpace(resource.Description))
                {
                    lines.Add($"  {resource.Description}");
                }

                if (includeSchema && !string.IsNullOrWhiteSpace(resource.MimeType))
                {
                    lines.Add($"  mimeType: {resource.MimeType}");
                }
            }
        }

        if (tools.Count > 0)
        {
            lines.Add("Tools:");
            foreach (var tool in tools)
            {
                var hints = new List<string>();
                if (tool.ReadOnlyHint)
                {
                    hints.Add("read-only");
                }

                if (tool.DestructiveHint)
                {
                    hints.Add("destructive");
                }

                if (tool.IdempotentHint)
                {
                    hints.Add("idempotent");
                }

                if (tool.OpenWorldHint)
                {
                    hints.Add("open-world");
                }

                var hintSuffix = hints.Count > 0 ? $" [{string.Join(", ", hints)}]" : string.Empty;
                lines.Add($"- {tool.FullyQualifiedName}{hintSuffix}");
                if (!string.IsNullOrWhiteSpace(tool.Description))
                {
                    lines.Add($"  {tool.Description}");
                }

                if (includeSchema)
                {
                    lines.Add($"  schema: {tool.InputSchemaJson}");
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatServerHeader(McpServerDefinition server) =>
        $"Server {server.Name} ({server.Transport}, trust={server.Trust.ToString().ToLowerInvariant()})";

    private static string BuildConnectedMessage(string serverName, int toolCount, int promptCount, int resourceCount)
    {
        var parts = new List<string> { $"{toolCount} tool(s)" };
        if (promptCount > 0)
        {
            parts.Add($"{promptCount} prompt(s)");
        }

        if (resourceCount > 0)
        {
            parts.Add($"{resourceCount} resource(s)");
        }

        return $"Connected to MCP server '{serverName}' and discovered {string.Join(" and ", parts)}.";
    }

    private static JsonElement ExtractToolArguments(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("arguments", out var toolArguments))
        {
            return default;
        }

        return toolArguments.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? toolArguments
            : default;
    }

    private static string FormatCallResult(McpCallToolResponse result)
    {
        if (result.Content.Count == 0)
        {
            return result.IsError
                ? "MCP tool reported an error without any content."
                : "MCP tool completed without returning content.";
        }

        var rendered = result.Content
            .Select(FormatContentBlock)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        return rendered.Length == 0
            ? (result.IsError ? "MCP tool reported an error." : "MCP tool completed.")
            : string.Join(Environment.NewLine, rendered);
    }

    private static string FormatResourceResult(McpReadResourceResponse result)
    {
        if (result.Contents.Count == 0)
        {
            return "MCP resource read completed without returning contents.";
        }

        var rendered = result.Contents
            .Select(FormatResourceContent)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        return rendered.Length == 0
            ? "MCP resource read completed."
            : string.Join(Environment.NewLine, rendered);
    }

    private static string FormatPromptResult(McpGetPromptResponse result)
    {
        if (result.Messages.Count == 0)
        {
            return "MCP prompt completed without returning messages.";
        }

        var rendered = result.Messages
            .Select(FormatPromptMessage)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        return rendered.Length == 0
            ? "MCP prompt completed."
            : string.Join(Environment.NewLine, rendered);
    }

    private static string FormatContentBlock(JsonNode? block)
    {
        if (block is not JsonObject item)
        {
            return block?.ToJsonString(SerializerOptions) ?? string.Empty;
        }

        var type = item["type"]?.GetValue<string?>() ?? string.Empty;
        return type switch
        {
            "text" => item["text"]?.GetValue<string?>() ?? string.Empty,
            "image" or "audio" => $"[{type}:{item["mimeType"]?.GetValue<string?>() ?? "application/octet-stream"}]",
            "resource" => item["resource"]?["text"]?.GetValue<string?>()
                ?? item["resource"]?.ToJsonString(SerializerOptions)
                ?? item.ToJsonString(SerializerOptions),
            "resource_link" => item["uri"]?.GetValue<string?>() ?? item.ToJsonString(SerializerOptions),
            _ => item.ToJsonString(SerializerOptions)
        };
    }

    private static string FormatResourceContent(JsonNode? content)
    {
        if (content is not JsonObject item)
        {
            return content?.ToJsonString(SerializerOptions) ?? string.Empty;
        }

        if (item["text"]?.GetValue<string?>() is { Length: > 0 } text)
        {
            return text;
        }

        if (item["blob"]?.GetValue<string?>() is { Length: > 0 } blob)
        {
            var mimeType = item["mimeType"]?.GetValue<string?>() ?? "application/octet-stream";
            return $"[blob:{mimeType}] {blob}";
        }

        return item["uri"]?.GetValue<string?>()
            ?? item.ToJsonString(SerializerOptions);
    }

    private static string FormatPromptMessage(JsonNode? message)
    {
        if (message is not JsonObject item)
        {
            return message?.ToJsonString(SerializerOptions) ?? string.Empty;
        }

        var role = item["role"]?.GetValue<string?>() ?? "assistant";
        var content = item["content"];
        var rendered = FormatContentBlock(content);
        return string.IsNullOrWhiteSpace(rendered)
            ? role
            : $"[{role}] {rendered}";
    }

    private static IReadOnlyList<McpToolDefinition> ParseTools(string serverName, JsonArray? tools)
    {
        if (tools is null)
        {
            return [];
        }

        return tools
            .OfType<JsonObject>()
            .Select(tool => new McpToolDefinition
            {
                ServerName = serverName,
                Name = tool["name"]?.GetValue<string?>() ?? "unknown",
                FullyQualifiedName = $"mcp__{serverName}__{tool["name"]?.GetValue<string?>() ?? "unknown"}",
                Description = tool["description"]?.GetValue<string?>() ?? string.Empty,
                InputSchemaJson = tool["inputSchema"]?.ToJsonString(SerializerOptions) ?? "{}",
                ReadOnlyHint = tool["annotations"]?["readOnlyHint"]?.GetValue<bool?>() ?? false,
                DestructiveHint = tool["annotations"]?["destructiveHint"]?.GetValue<bool?>() ?? false,
                IdempotentHint = tool["annotations"]?["idempotentHint"]?.GetValue<bool?>() ?? false,
                OpenWorldHint = tool["annotations"]?["openWorldHint"]?.GetValue<bool?>() ?? false
            })
            .Where(static tool => !string.Equals(tool.Name, "unknown", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<McpPromptDefinition> ParsePrompts(string serverName, JsonArray? prompts)
    {
        if (prompts is null)
        {
            return [];
        }

        return prompts
            .OfType<JsonObject>()
            .Select(prompt => new McpPromptDefinition
            {
                ServerName = serverName,
                Name = prompt["name"]?.GetValue<string?>() ?? "unknown",
                Description = prompt["description"]?.GetValue<string?>() ?? string.Empty,
                ArgumentsJson = prompt["arguments"]?.ToJsonString(SerializerOptions) ?? "[]"
            })
            .Where(static prompt => !string.Equals(prompt.Name, "unknown", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<McpResourceDefinition> ParseResources(string serverName, JsonArray? resources)
    {
        if (resources is null)
        {
            return [];
        }

        return resources
            .OfType<JsonObject>()
            .Select(resource => new McpResourceDefinition
            {
                ServerName = serverName,
                Uri = resource["uri"]?.GetValue<string?>() ?? "unknown",
                Name = resource["name"]?.GetValue<string?>()
                    ?? resource["uri"]?.GetValue<string?>()
                    ?? "unknown",
                Description = resource["description"]?.GetValue<string?>() ?? string.Empty,
                MimeType = resource["mimeType"]?.GetValue<string?>() ?? string.Empty
            })
            .Where(static resource => !string.Equals(resource.Uri, "unknown", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool? TryGetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;

    private static bool TryExtractAccessToken(string payload, out string accessToken)
    {
        accessToken = string.Empty;
        try
        {
            var node = JsonNode.Parse(payload) as JsonObject;
            var candidate = node?["access_token"]?.GetValue<string?>()
                ?? node?["accessToken"]?.GetValue<string?>()
                ?? node?["token"]?.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                accessToken = candidate;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private interface IMcpTransport : IAsyncDisposable
    {
        bool SupportsPrompts { get; }

        bool SupportsResources { get; }

        Task InitializeAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(CancellationToken cancellationToken);

        Task<McpReadResourceResponse> ReadResourceAsync(string uri, CancellationToken cancellationToken);

        Task<McpGetPromptResponse> GetPromptAsync(string promptName, JsonElement arguments, CancellationToken cancellationToken);

        Task<McpCallToolResponse> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken);
    }

    private sealed class McpProtocolSession : IAsyncDisposable
    {
        private readonly IMcpTransport transport;

        private McpProtocolSession(IMcpTransport transport)
        {
            this.transport = transport;
        }

        /// <summary>
        /// Gets the supports prompts
        /// </summary>
        public bool SupportsPrompts => transport.SupportsPrompts;

        /// <summary>
        /// Gets the supports resources
        /// </summary>
        public bool SupportsResources => transport.SupportsResources;

        /// <summary>
        /// Connects async
        /// </summary>
        /// <param name="server">The server</param>
        /// <param name="httpClient">The http client</param>
        /// <param name="tokenStore">The token store</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp protocol session</returns>
        public static async Task<McpProtocolSession> ConnectAsync(
            McpServerDefinition server,
            HttpClient httpClient,
            IMcpTokenStore tokenStore,
            CancellationToken cancellationToken)
        {
            IMcpTransport transport = server.Transport.ToLowerInvariant() switch
            {
                "stdio" => new StdioMcpTransport(server),
                "http" => new HttpMcpTransport(server, httpClient, tokenStore),
                "sse" => new SseMcpTransport(server, httpClient, tokenStore),
                _ => throw new InvalidOperationException($"Unsupported MCP transport '{server.Transport}'.")
            };

            try
            {
                await transport.InitializeAsync(cancellationToken);
                return new McpProtocolSession(transport);
            }
            catch
            {
                await transport.DisposeAsync();
                throw;
            }
        }

        /// <summary>
        /// Lists tools async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp tool definition</returns>
        public Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken) =>
            transport.ListToolsAsync(cancellationToken);

        /// <summary>
        /// Lists prompts async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp prompt definition</returns>
        public Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken) =>
            transport.ListPromptsAsync(cancellationToken);

        /// <summary>
        /// Lists resources async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp resource definition</returns>
        public Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(CancellationToken cancellationToken) =>
            transport.ListResourcesAsync(cancellationToken);

        /// <summary>
        /// Reads resource async
        /// </summary>
        /// <param name="uri">The uri</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp read resource response</returns>
        public Task<McpReadResourceResponse> ReadResourceAsync(
            string uri,
            CancellationToken cancellationToken) =>
            transport.ReadResourceAsync(uri, cancellationToken);

        /// <summary>
        /// Gets prompt async
        /// </summary>
        /// <param name="promptName">The prompt name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp get prompt response</returns>
        public Task<McpGetPromptResponse> GetPromptAsync(
            string promptName,
            JsonElement arguments,
            CancellationToken cancellationToken) =>
            transport.GetPromptAsync(promptName, arguments, cancellationToken);

        /// <summary>
        /// Executes call tool async
        /// </summary>
        /// <param name="toolName">The tool name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp call tool response</returns>
        public Task<McpCallToolResponse> CallToolAsync(
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken) =>
            transport.CallToolAsync(toolName, arguments, cancellationToken);

        /// <summary>
        /// Executes dispose async
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public ValueTask DisposeAsync() => transport.DisposeAsync();
    }

    private sealed class HttpMcpTransport : IMcpTransport
    {
        private readonly McpServerDefinition server;
        private readonly HttpClient client;
        private readonly IMcpTokenStore tokenStore;
        private bool supportsPrompts;
        private bool supportsResources;
        private int nextId = 1;

        /// <summary>
        /// Initializes a new instance of the HttpMcpTransport class
        /// </summary>
        /// <param name="server">The server</param>
        /// <param name="client">The client</param>
        /// <param name="tokenStore">The token store</param>
        public HttpMcpTransport(
            McpServerDefinition server,
            HttpClient client,
            IMcpTokenStore tokenStore)
        {
            this.server = server;
            this.client = client;
            this.tokenStore = tokenStore;
        }

        /// <summary>
        /// Gets the supports prompts
        /// </summary>
        public bool SupportsPrompts => supportsPrompts;

        /// <summary>
        /// Gets the supports resources
        /// </summary>
        public bool SupportsResources => supportsResources;

        /// <summary>
        /// Executes initialize async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var initializeResult = await SendRequestAsync(
                "initialize",
                new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject
                    {
                        ["name"] = "qwen-code-desktop",
                        ["version"] = "0.1.0"
                    }
                },
                cancellationToken);
            supportsPrompts = initializeResult["capabilities"]?["prompts"] is not null;
            supportsResources = initializeResult["capabilities"]?["resources"] is not null;

            await SendNotificationAsync("notifications/initialized", new JsonObject(), cancellationToken);
        }

        /// <summary>
        /// Lists tools async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp tool definition</returns>
        public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            return ParseTools(server.Name, result["tools"] as JsonArray);
        }

        /// <summary>
        /// Lists prompts async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp prompt definition</returns>
        public async Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            if (!supportsPrompts)
            {
                return [];
            }

            var result = await SendRequestAsync("prompts/list", new JsonObject(), cancellationToken);
            return ParsePrompts(server.Name, result["prompts"] as JsonArray);
        }

        /// <summary>
        /// Lists resources async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp resource definition</returns>
        public async Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            if (!supportsResources)
            {
                return [];
            }

            var result = await SendRequestAsync("resources/list", new JsonObject(), cancellationToken);
            return ParseResources(server.Name, result["resources"] as JsonArray);
        }

        /// <summary>
        /// Executes call tool async
        /// </summary>
        /// <param name="toolName">The tool name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp call tool response</returns>
        public async Task<McpCallToolResponse> CallToolAsync(
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync(
                "tools/call",
                new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? new JsonObject()
                        : JsonNode.Parse(arguments.GetRawText())
                },
                cancellationToken);

            return new McpCallToolResponse(
                result["content"] as JsonArray ?? [],
                result["isError"]?.GetValue<bool?>() ?? false);
        }

        /// <summary>
        /// Reads resource async
        /// </summary>
        /// <param name="uri">The uri</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp read resource response</returns>
        public async Task<McpReadResourceResponse> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            if (!supportsResources)
            {
                throw new InvalidOperationException("MCP server does not support resources.");
            }

            var result = await SendRequestAsync(
                "resources/read",
                new JsonObject
                {
                    ["uri"] = uri
                },
                cancellationToken);

            return new McpReadResourceResponse(result["contents"] as JsonArray ?? []);
        }

        /// <summary>
        /// Gets prompt async
        /// </summary>
        /// <param name="promptName">The prompt name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp get prompt response</returns>
        public async Task<McpGetPromptResponse> GetPromptAsync(
            string promptName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            if (!supportsPrompts)
            {
                throw new InvalidOperationException("MCP server does not support prompts.");
            }

            var result = await SendRequestAsync(
                "prompts/get",
                new JsonObject
                {
                    ["name"] = promptName,
                    ["arguments"] = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? new JsonObject()
                        : JsonNode.Parse(arguments.GetRawText())
                },
                cancellationToken);

            return new McpGetPromptResponse(result["messages"] as JsonArray ?? []);
        }

        /// <summary>
        /// Executes dispose async
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task<JsonObject> SendRequestAsync(
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            var requestNode = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = nextId++,
                ["method"] = method,
                ["params"] = parameters
            };

            using var request = await CreateHttpRequestMessageAsync(requestNode, cancellationToken);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResult(payload, method);
        }

        private async Task SendNotificationAsync(
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            var requestNode = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters
            };

            using var request = await CreateHttpRequestMessageAsync(requestNode, cancellationToken);
            using var _ = await client.SendAsync(request, cancellationToken);
        }

        private async Task<HttpRequestMessage> CreateHttpRequestMessageAsync(
            JsonObject payload,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(server.CommandOrUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException($"MCP server URL '{server.CommandOrUrl}' is invalid.");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(payload.ToJsonString(SerializerOptions), Encoding.UTF8, "application/json")
            };

            foreach (var header in server.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var tokenPayload = await tokenStore.GetTokenAsync(server.Name, cancellationToken);
            if (!string.IsNullOrWhiteSpace(tokenPayload) &&
                TryExtractAccessToken(tokenPayload, out var accessToken) &&
                request.Headers.Authorization is null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return request;
        }
    }

    private sealed class StdioMcpTransport : IMcpTransport
    {
        private readonly McpServerDefinition server;
        private readonly Process process;
        private readonly StreamWriter stdin;
        private readonly StreamReader stdout;
        private readonly StringBuilder stderr = new();
        private bool supportsPrompts;
        private bool supportsResources;
        private int nextId = 1;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the StdioMcpTransport class
        /// </summary>
        /// <param name="server">The server</param>
        public StdioMcpTransport(McpServerDefinition server)
        {
            this.server = server;
            process = StartProcess(server);
            stdin = process.StandardInput;
            stdout = process.StandardOutput;

            _ = Task.Run(async () =>
            {
                while (!process.HasExited)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (line is null)
                    {
                        break;
                    }

                    lock (stderr)
                    {
                        stderr.AppendLine(line);
                    }
                }
            });
        }

        /// <summary>
        /// Gets the supports prompts
        /// </summary>
        public bool SupportsPrompts => supportsPrompts;

        /// <summary>
        /// Gets the supports resources
        /// </summary>
        public bool SupportsResources => supportsResources;

        /// <summary>
        /// Executes initialize async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            var initializeResult = await SendRequestAsync(
                "initialize",
                new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject
                    {
                        ["name"] = "qwen-code-desktop",
                        ["version"] = "0.1.0"
                    }
                },
                cancellationToken);
            supportsPrompts = initializeResult["capabilities"]?["prompts"] is not null;
            supportsResources = initializeResult["capabilities"]?["resources"] is not null;

            await SendNotificationAsync("notifications/initialized", new JsonObject(), cancellationToken);
        }

        /// <summary>
        /// Lists tools async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp tool definition</returns>
        public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            return ParseTools(server.Name, result["tools"] as JsonArray);
        }

        /// <summary>
        /// Lists prompts async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp prompt definition</returns>
        public async Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            if (!supportsPrompts)
            {
                return [];
            }

            var result = await SendRequestAsync("prompts/list", new JsonObject(), cancellationToken);
            return ParsePrompts(server.Name, result["prompts"] as JsonArray);
        }

        /// <summary>
        /// Lists resources async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp resource definition</returns>
        public async Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            if (!supportsResources)
            {
                return [];
            }

            var result = await SendRequestAsync("resources/list", new JsonObject(), cancellationToken);
            return ParseResources(server.Name, result["resources"] as JsonArray);
        }

        /// <summary>
        /// Executes call tool async
        /// </summary>
        /// <param name="toolName">The tool name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp call tool response</returns>
        public async Task<McpCallToolResponse> CallToolAsync(
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync(
                "tools/call",
                new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? new JsonObject()
                        : JsonNode.Parse(arguments.GetRawText())
                },
                cancellationToken);

            return new McpCallToolResponse(
                result["content"] as JsonArray ?? [],
                result["isError"]?.GetValue<bool?>() ?? false);
        }

        /// <summary>
        /// Reads resource async
        /// </summary>
        /// <param name="uri">The uri</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp read resource response</returns>
        public async Task<McpReadResourceResponse> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            if (!supportsResources)
            {
                throw new InvalidOperationException("MCP server does not support resources.");
            }

            var result = await SendRequestAsync(
                "resources/read",
                new JsonObject
                {
                    ["uri"] = uri
                },
                cancellationToken);

            return new McpReadResourceResponse(result["contents"] as JsonArray ?? []);
        }

        /// <summary>
        /// Gets prompt async
        /// </summary>
        /// <param name="promptName">The prompt name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp get prompt response</returns>
        public async Task<McpGetPromptResponse> GetPromptAsync(
            string promptName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            if (!supportsPrompts)
            {
                throw new InvalidOperationException("MCP server does not support prompts.");
            }

            var result = await SendRequestAsync(
                "prompts/get",
                new JsonObject
                {
                    ["name"] = promptName,
                    ["arguments"] = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? new JsonObject()
                        : JsonNode.Parse(arguments.GetRawText())
                },
                cancellationToken);

            return new McpGetPromptResponse(result["messages"] as JsonArray ?? []);
        }

        /// <summary>
        /// Executes dispose async
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            try
            {
                await stdin.DisposeAsync();
            }
            catch
            {
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
            catch
            {
            }

            process.Dispose();
        }

        private async Task<JsonObject> SendRequestAsync(
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(server.TimeoutMs ?? 5000));

            var id = nextId++;
            var requestNode = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            };

            await stdin.WriteLineAsync(requestNode.ToJsonString(SerializerOptions).AsMemory(), timeoutSource.Token);
            await stdin.FlushAsync(timeoutSource.Token);

            while (!timeoutSource.IsCancellationRequested)
            {
                var line = await stdout.ReadLineAsync(timeoutSource.Token);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var response = JsonNode.Parse(line) as JsonObject
                    ?? throw new InvalidOperationException("MCP stdio server returned invalid JSON.");

                if (response["id"]?.GetValue<int?>() != id)
                {
                    continue;
                }

                if (response["error"] is JsonObject error)
                {
                    var message = error["message"]?.GetValue<string?>()
                        ?? $"MCP stdio server returned an error for '{method}'.";
                    var stderrText = GetStderrText();
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(stderrText) ? message : $"{message} {stderrText}".Trim());
                }

                return response["result"] as JsonObject
                    ?? throw new InvalidOperationException($"MCP stdio server did not return a JSON-RPC result for '{method}'.");
            }

            throw new OperationCanceledException(timeoutSource.Token);
        }

        private async Task SendNotificationAsync(
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            var requestNode = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters
            };

            await stdin.WriteLineAsync(requestNode.ToJsonString(SerializerOptions).AsMemory(), cancellationToken);
            await stdin.FlushAsync(cancellationToken);
        }

        private string GetStderrText()
        {
            lock (stderr)
            {
                return stderr.ToString().Trim();
            }
        }

        private static Process StartProcess(McpServerDefinition server)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = server.CommandOrUrl,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in server.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var environmentVariable in server.EnvironmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }

            var process = new Process { StartInfo = startInfo };
            process.Start();
            return process;
        }
    }

    private sealed class SseMcpTransport : IMcpTransport
    {
        private readonly McpServerDefinition server;
        private readonly HttpClient client;
        private readonly IMcpTokenStore tokenStore;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonObject>> pendingRequests = new();
        private readonly SemaphoreSlim lifecycleGate = new(1, 1);
        private CancellationTokenSource? readerCancellation;
        private Task? readerTask;
        private HttpResponseMessage? streamResponse;
        private StreamReader? streamReader;
        private Uri? messageEndpoint;
        private bool disposed;
        private bool supportsPrompts;
        private bool supportsResources;
        private int nextId = 1;

        /// <summary>
        /// Initializes a new instance of the SseMcpTransport class
        /// </summary>
        /// <param name="server">The server</param>
        /// <param name="client">The client</param>
        /// <param name="tokenStore">The token store</param>
        public SseMcpTransport(
            McpServerDefinition server,
            HttpClient client,
            IMcpTokenStore tokenStore)
        {
            this.server = server;
            this.client = client;
            this.tokenStore = tokenStore;
        }

        /// <summary>
        /// Gets the supports prompts
        /// </summary>
        public bool SupportsPrompts => supportsPrompts;

        /// <summary>
        /// Gets the supports resources
        /// </summary>
        public bool SupportsResources => supportsResources;

        /// <summary>
        /// Executes initialize async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await lifecycleGate.WaitAsync(cancellationToken);
            try
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(SseMcpTransport));
                }

                readerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                using var request = await CreateStreamRequestAsync(cancellationToken);
                streamResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                streamResponse.EnsureSuccessStatusCode();

                var stream = await streamResponse.Content.ReadAsStreamAsync(cancellationToken);
                streamReader = new StreamReader(stream);
                readerTask = Task.Run(() => PumpEventsAsync(streamReader, readerCancellation.Token), CancellationToken.None);

                var defaultEndpoint = new Uri(server.CommandOrUrl, UriKind.Absolute);
                messageEndpoint ??= defaultEndpoint;

                var initializeResult = await SendRequestAsync(
                    "initialize",
                    new JsonObject
                    {
                        ["protocolVersion"] = "2025-06-18",
                        ["capabilities"] = new JsonObject(),
                        ["clientInfo"] = new JsonObject
                        {
                            ["name"] = "qwen-code-desktop",
                            ["version"] = "0.1.0"
                        }
                    },
                    cancellationToken);
                supportsPrompts = initializeResult["capabilities"]?["prompts"] is not null;
                supportsResources = initializeResult["capabilities"]?["resources"] is not null;

                await SendNotificationAsync("notifications/initialized", new JsonObject(), cancellationToken);
            }
            finally
            {
                lifecycleGate.Release();
            }
        }

        /// <summary>
        /// Lists tools async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp tool definition</returns>
        public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            return ParseTools(server.Name, result["tools"] as JsonArray);
        }

        /// <summary>
        /// Lists prompts async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp prompt definition</returns>
        public async Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            if (!supportsPrompts)
            {
                return [];
            }

            var result = await SendRequestAsync("prompts/list", new JsonObject(), cancellationToken);
            return ParsePrompts(server.Name, result["prompts"] as JsonArray);
        }

        /// <summary>
        /// Lists resources async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp resource definition</returns>
        public async Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            if (!supportsResources)
            {
                return [];
            }

            var result = await SendRequestAsync("resources/list", new JsonObject(), cancellationToken);
            return ParseResources(server.Name, result["resources"] as JsonArray);
        }

        /// <summary>
        /// Executes call tool async
        /// </summary>
        /// <param name="toolName">The tool name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp call tool response</returns>
        public async Task<McpCallToolResponse> CallToolAsync(
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync(
                "tools/call",
                new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? new JsonObject()
                        : JsonNode.Parse(arguments.GetRawText())
                },
                cancellationToken);

            return new McpCallToolResponse(
                result["content"] as JsonArray ?? [],
                result["isError"]?.GetValue<bool?>() ?? false);
        }

        /// <summary>
        /// Reads resource async
        /// </summary>
        /// <param name="uri">The uri</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp read resource response</returns>
        public async Task<McpReadResourceResponse> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            if (!supportsResources)
            {
                throw new InvalidOperationException("MCP server does not support resources.");
            }

            var result = await SendRequestAsync(
                "resources/read",
                new JsonObject
                {
                    ["uri"] = uri
                },
                cancellationToken);

            return new McpReadResourceResponse(result["contents"] as JsonArray ?? []);
        }

        /// <summary>
        /// Gets prompt async
        /// </summary>
        /// <param name="promptName">The prompt name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp get prompt response</returns>
        public async Task<McpGetPromptResponse> GetPromptAsync(
            string promptName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            if (!supportsPrompts)
            {
                throw new InvalidOperationException("MCP server does not support prompts.");
            }

            var result = await SendRequestAsync(
                "prompts/get",
                new JsonObject
                {
                    ["name"] = promptName,
                    ["arguments"] = arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        ? new JsonObject()
                        : JsonNode.Parse(arguments.GetRawText())
                },
                cancellationToken);

            return new McpGetPromptResponse(result["messages"] as JsonArray ?? []);
        }

        /// <summary>
        /// Executes dispose async
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (readerCancellation is not null)
            {
                try
                {
                    await readerCancellation.CancelAsync();
                }
                catch
                {
                }
            }

            if (readerTask is not null)
            {
                try
                {
                    await readerTask;
                }
                catch
                {
                }
            }

            foreach (var pending in pendingRequests)
            {
                pending.Value.TrySetCanceled();
            }

            streamReader?.Dispose();
            streamResponse?.Dispose();
            lifecycleGate.Dispose();
            readerCancellation?.Dispose();
        }

        private async Task<JsonObject> SendRequestAsync(
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            var id = Interlocked.Increment(ref nextId);
            var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            pendingRequests[id] = completion;

            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            };

            try
            {
                using var request = await CreateMessageRequestAsync(payload, cancellationToken);
                using var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(server.TimeoutMs ?? 10000));
                using var registration = timeoutSource.Token.Register(() => completion.TrySetCanceled(timeoutSource.Token));
                return await completion.Task.WaitAsync(timeoutSource.Token);
            }
            finally
            {
                pendingRequests.TryRemove(id, out _);
            }
        }

        private async Task SendNotificationAsync(
            string method,
            JsonObject parameters,
            CancellationToken cancellationToken)
        {
            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters
            };

            using var request = await CreateMessageRequestAsync(payload, cancellationToken);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task<HttpRequestMessage> CreateStreamRequestAsync(CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(server.CommandOrUrl, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException($"MCP SSE URL '{server.CommandOrUrl}' is invalid.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Accept.ParseAdd("text/event-stream");
            await ApplyHeadersAsync(request, cancellationToken);
            return request;
        }

        private async Task<HttpRequestMessage> CreateMessageRequestAsync(JsonObject payload, CancellationToken cancellationToken)
        {
            var target = messageEndpoint
                ?? (Uri.TryCreate(server.CommandOrUrl, UriKind.Absolute, out var fallback) ? fallback : null)
                ?? throw new InvalidOperationException($"MCP SSE URL '{server.CommandOrUrl}' is invalid.");

            var request = new HttpRequestMessage(HttpMethod.Post, target)
            {
                Content = new StringContent(payload.ToJsonString(SerializerOptions), Encoding.UTF8, "application/json")
            };
            await ApplyHeadersAsync(request, cancellationToken);
            return request;
        }

        private async Task ApplyHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            foreach (var header in server.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var tokenPayload = await tokenStore.GetTokenAsync(server.Name, cancellationToken);
            if (!string.IsNullOrWhiteSpace(tokenPayload) &&
                TryExtractAccessToken(tokenPayload, out var accessToken) &&
                request.Headers.Authorization is null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        private async Task PumpEventsAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var eventName = string.Empty;
            var dataBuilder = new StringBuilder();

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    ProcessEvent(eventName, dataBuilder.ToString());
                    eventName = string.Empty;
                    dataBuilder.Clear();
                    continue;
                }

                if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                {
                    eventName = line["event:".Length..].Trim();
                    continue;
                }

                if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    if (dataBuilder.Length > 0)
                    {
                        dataBuilder.AppendLine();
                    }

                    dataBuilder.Append(line["data:".Length..].TrimStart());
                }
            }
        }

        private void ProcessEvent(string eventName, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            if (string.Equals(eventName, "endpoint", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(data, UriKind.Absolute, out var absolute))
                {
                    messageEndpoint = absolute;
                    return;
                }

                if (Uri.TryCreate(new Uri(server.CommandOrUrl, UriKind.Absolute), data, out var relative))
                {
                    messageEndpoint = relative;
                }

                return;
            }

            try
            {
                var payload = JsonNode.Parse(data) as JsonObject;
                if (payload is null)
                {
                    return;
                }

                if (payload["id"]?.GetValue<int?>() is { } id &&
                    pendingRequests.TryGetValue(id, out var pending))
                {
                    if (payload["error"] is JsonObject error)
                    {
                        pending.TrySetException(new InvalidOperationException(
                            error["message"]?.GetValue<string?>() ?? "MCP SSE server returned an error."));
                        return;
                    }

                    pending.TrySetResult(payload["result"] as JsonObject ?? new JsonObject());
                }
            }
            catch
            {
            }
        }
    }

    private sealed class PooledSession : IAsyncDisposable
    {
        private readonly McpServerDefinition server;
        private readonly SemaphoreSlim gate = new(1, 1);
        private McpProtocolSession? session;

        /// <summary>
        /// Initializes a new instance of the PooledSession class
        /// </summary>
        /// <param name="server">The server</param>
        public PooledSession(McpServerDefinition server)
        {
            this.server = server;
        }

        /// <summary>
        /// Gets the supports prompts
        /// </summary>
        public bool SupportsPrompts => session?.SupportsPrompts ?? false;

        /// <summary>
        /// Gets the supports resources
        /// </summary>
        public bool SupportsResources => session?.SupportsResources ?? false;

        /// <summary>
        /// Gets or sets the last discovery utc
        /// </summary>
        public DateTimeOffset? LastDiscoveryUtc { get; private set; }

        /// <summary>
        /// Executes ensure connected async
        /// </summary>
        /// <param name="connectFactory">The connect factory</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task EnsureConnectedAsync(
            Func<Task<McpProtocolSession>> connectFactory,
            CancellationToken cancellationToken)
        {
            if (session is not null)
            {
                return;
            }

            await gate.WaitAsync(cancellationToken);
            try
            {
                session ??= await connectFactory();
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Lists tools async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp tool definition</returns>
        public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException($"MCP server '{server.Name}' is not connected.");
            }

            await gate.WaitAsync(cancellationToken);
            try
            {
                LastDiscoveryUtc = DateTimeOffset.UtcNow;
                return await session.ListToolsAsync(cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Lists prompts async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp prompt definition</returns>
        public async Task<IReadOnlyList<McpPromptDefinition>> ListPromptsAsync(CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException($"MCP server '{server.Name}' is not connected.");
            }

            await gate.WaitAsync(cancellationToken);
            try
            {
                LastDiscoveryUtc = DateTimeOffset.UtcNow;
                return await session.ListPromptsAsync(cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Lists resources async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to i read only list mcp resource definition</returns>
        public async Task<IReadOnlyList<McpResourceDefinition>> ListResourcesAsync(CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException($"MCP server '{server.Name}' is not connected.");
            }

            await gate.WaitAsync(cancellationToken);
            try
            {
                LastDiscoveryUtc = DateTimeOffset.UtcNow;
                return await session.ListResourcesAsync(cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Reads resource async
        /// </summary>
        /// <param name="uri">The uri</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp read resource response</returns>
        public async Task<McpReadResourceResponse> ReadResourceAsync(
            string uri,
            CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException($"MCP server '{server.Name}' is not connected.");
            }

            await gate.WaitAsync(cancellationToken);
            try
            {
                return await session.ReadResourceAsync(uri, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Gets prompt async
        /// </summary>
        /// <param name="promptName">The prompt name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp get prompt response</returns>
        public async Task<McpGetPromptResponse> GetPromptAsync(
            string promptName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException($"MCP server '{server.Name}' is not connected.");
            }

            await gate.WaitAsync(cancellationToken);
            try
            {
                return await session.GetPromptAsync(promptName, arguments, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Executes call tool async
        /// </summary>
        /// <param name="toolName">The tool name</param>
        /// <param name="arguments">The arguments</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to mcp call tool response</returns>
        public async Task<McpCallToolResponse> CallToolAsync(
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken)
        {
            if (session is null)
            {
                throw new InvalidOperationException($"MCP server '{server.Name}' is not connected.");
            }

            await gate.WaitAsync(cancellationToken);
            try
            {
                return await session.CallToolAsync(toolName, arguments, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Executes dispose async
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async ValueTask DisposeAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (session is not null)
                {
                    await session.DisposeAsync();
                    session = null;
                }
            }
            finally
            {
                gate.Release();
                gate.Dispose();
            }
        }
    }

    private static JsonObject ParseResult(string payload, string method)
    {
        var response = JsonNode.Parse(payload) as JsonObject
            ?? throw new InvalidOperationException("MCP server returned an invalid JSON-RPC response.");

        if (response["error"] is JsonObject error)
        {
            throw new InvalidOperationException(
                error["message"]?.GetValue<string?>() ?? $"MCP server returned an error for '{method}'.");
        }

        return response["result"] as JsonObject
            ?? throw new InvalidOperationException($"MCP server did not return a JSON-RPC result for '{method}'.");
    }

    private sealed record McpCallToolResponse(JsonArray Content, bool IsError);

    private sealed record McpReadResourceResponse(JsonArray Contents);

    private sealed record McpGetPromptResponse(JsonArray Messages);
}
