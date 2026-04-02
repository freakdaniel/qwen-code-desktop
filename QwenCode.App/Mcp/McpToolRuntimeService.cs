using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Mcp;

public sealed class McpToolRuntimeService(
    IMcpRegistry registry,
    IMcpTokenStore tokenStore,
    HttpClient httpClient) : IMcpToolRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> DescribeAsync(
        WorkspacePaths paths,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
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
                var tools = await ListToolsForServerAsync(server, cancellationToken);
                sections.Add(FormatServerSummary(server, tools, includeSchema));
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

    public async Task<McpToolDefinition> ResolveToolAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        CancellationToken cancellationToken = default)
    {
        var server = ResolveServer(paths, serverName);
        var tools = await ListToolsForServerAsync(server, cancellationToken);
        var resolved = tools.FirstOrDefault(item => string.Equals(item.Name, toolName, StringComparison.OrdinalIgnoreCase));
        if (resolved is null)
        {
            throw new InvalidOperationException($"MCP tool '{toolName}' was not found on server '{serverName}'.");
        }

        return resolved;
    }

    public async Task<McpToolInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        string serverName,
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        var server = ResolveServer(paths, serverName);
        _ = await ResolveToolAsync(paths, serverName, toolName, cancellationToken);

        await using var session = await McpProtocolSession.ConnectAsync(server, httpClient, tokenStore, cancellationToken);
        var result = await session.CallToolAsync(toolName, ExtractToolArguments(arguments), cancellationToken);

        return new McpToolInvocationResult
        {
            ServerName = serverName,
            ToolName = toolName,
            Output = FormatCallResult(result),
            IsError = result.IsError
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
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        await using var session = await McpProtocolSession.ConnectAsync(server, httpClient, tokenStore, cancellationToken);
        var tools = await session.ListToolsAsync(cancellationToken);

        return tools
            .Where(tool => server.IncludeTools.Count == 0 || server.IncludeTools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
            .Where(tool => !server.ExcludeTools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(static tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string FormatServerSummary(
        McpServerDefinition server,
        IReadOnlyList<McpToolDefinition> tools,
        bool includeSchema)
    {
        var lines = new List<string> { FormatServerHeader(server) };

        if (!string.IsNullOrWhiteSpace(server.Description))
        {
            lines.Add(server.Description);
        }

        if (tools.Count == 0)
        {
            lines.Add("No tools discovered.");
            return string.Join(Environment.NewLine, lines);
        }

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

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatServerHeader(McpServerDefinition server) =>
        $"Server {server.Name} ({server.Transport}, trust={server.Trust.ToString().ToLowerInvariant()})";

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
        Task InitializeAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken);

        Task<McpCallToolResponse> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken);
    }

    private sealed class McpProtocolSession : IAsyncDisposable
    {
        private readonly IMcpTransport transport;

        private McpProtocolSession(IMcpTransport transport)
        {
            this.transport = transport;
        }

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
                "sse" => throw new InvalidOperationException("SSE MCP execution is not implemented yet in the native C# runtime."),
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

        public Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken) =>
            transport.ListToolsAsync(cancellationToken);

        public Task<McpCallToolResponse> CallToolAsync(
            string toolName,
            JsonElement arguments,
            CancellationToken cancellationToken) =>
            transport.CallToolAsync(toolName, arguments, cancellationToken);

        public ValueTask DisposeAsync() => transport.DisposeAsync();
    }

    private sealed class HttpMcpTransport : IMcpTransport
    {
        private readonly McpServerDefinition server;
        private readonly HttpClient client;
        private readonly IMcpTokenStore tokenStore;
        private int nextId = 1;

        public HttpMcpTransport(
            McpServerDefinition server,
            HttpClient client,
            IMcpTokenStore tokenStore)
        {
            this.server = server;
            this.client = client;
            this.tokenStore = tokenStore;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _ = await SendRequestAsync(
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

            await SendNotificationAsync("notifications/initialized", new JsonObject(), cancellationToken);
        }

        public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            return ParseTools(server.Name, result["tools"] as JsonArray);
        }

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
        private int nextId = 1;
        private bool disposed;

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

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            _ = await SendRequestAsync(
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

            await SendNotificationAsync("notifications/initialized", new JsonObject(), cancellationToken);
        }

        public async Task<IReadOnlyList<McpToolDefinition>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            return ParseTools(server.Name, result["tools"] as JsonArray);
        }

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
}
