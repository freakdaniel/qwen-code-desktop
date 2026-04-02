using System.Net.Sockets;

namespace QwenCode.Tests.Tools;

public sealed class McpToolRuntimeTests
{
    [Fact]
    public async Task McpToolRuntimeService_DescribeAsync_ListsDiscoveredTools()
    {
        await using var server = await FakeMcpHttpServer.StartAsync(
            [
                CreateTool("read-doc", "Reads docs", readOnlyHint: true),
                CreateTool("write-doc", "Writes docs", destructiveHint: true)
            ]);

        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (paths, environment, runtimeProfileService, registry, runtime) = CreateRuntime(root);
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "docs",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = server.Url,
                Trust = false
            });

            var output = await runtime.DescribeAsync(paths, JsonDocument.Parse("""{"server_name":"docs","include_schema":true}""").RootElement);

            Assert.Contains("Server docs (http, trust=false)", output);
            Assert.Contains("mcp__docs__read-doc [read-only]", output);
            Assert.Contains("mcp__docs__write-doc [destructive]", output);
            Assert.Contains("\"type\":\"object\"", output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_AllowsReadOnlyMcpToolsWithoutManualApproval()
    {
        await using var server = await FakeMcpHttpServer.StartAsync(
            [CreateTool("read-doc", "Reads docs", readOnlyHint: true)],
            static (toolName, arguments) =>
                new JsonObject
                {
                    ["content"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = $"read result for {toolName}: {arguments?["path"]?.GetValue<string?>() ?? "n/a"}"
                    }),
                    ["isError"] = false
                });

        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-readonly-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (paths, environment, runtimeProfileService, registry, runtime) = CreateRuntime(root);
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "docs",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = server.Url
            });

            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                new InMemoryCronScheduler(),
                mcpToolRuntime: runtime);

            var result = await host.ExecuteAsync(paths, new ExecuteNativeToolRequest
            {
                ToolName = "mcp-tool",
                ArgumentsJson = """{"server_name":"docs","tool_name":"read-doc","arguments":{"path":"README.md"}}"""
            });

            Assert.Equal("completed", result.Status);
            Assert.Equal("allow", result.ApprovalState);
            Assert.Contains("read result for read-doc", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_GatesWritableMcpToolsUntilApproved()
    {
        await using var server = await FakeMcpHttpServer.StartAsync(
            [CreateTool("write-doc", "Writes docs")],
            static (toolName, arguments) =>
                new JsonObject
                {
                    ["content"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = $"write result for {toolName}: {arguments?["path"]?.GetValue<string?>() ?? "n/a"}"
                    }),
                    ["isError"] = false
                });

        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-write-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (paths, environment, runtimeProfileService, registry, runtime) = CreateRuntime(root);
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "docs",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = server.Url
            });

            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                new InMemoryCronScheduler(),
                mcpToolRuntime: runtime);

            var gated = await host.ExecuteAsync(paths, new ExecuteNativeToolRequest
            {
                ToolName = "mcp-tool",
                ArgumentsJson = """{"server_name":"docs","tool_name":"write-doc","arguments":{"path":"README.md"}}"""
            });

            var approved = await host.ExecuteAsync(paths, new ExecuteNativeToolRequest
            {
                ToolName = "mcp-tool",
                ApproveExecution = true,
                ArgumentsJson = """{"server_name":"docs","tool_name":"write-doc","arguments":{"path":"README.md"}}"""
            });

            Assert.Equal("approval-required", gated.Status);
            Assert.Equal("ask", gated.ApprovalState);
            Assert.Contains("Requires confirmation", gated.ErrorMessage);
            Assert.Equal("completed", approved.Status);
            Assert.Contains("write result for write-doc", approved.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (WorkspacePaths Paths, FakeDesktopEnvironmentPaths Environment, QwenRuntimeProfileService RuntimeProfileService, McpRegistryService Registry, McpToolRuntimeService Runtime) CreateRuntime(string root)
    {
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");

        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        var paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
        var environment = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
        var runtimeProfileService = new QwenRuntimeProfileService(environment);
        var tokenStore = new FileMcpTokenStore(environment);
        var registry = new McpRegistryService(runtimeProfileService, tokenStore);
        var runtime = new McpToolRuntimeService(registry, tokenStore, new HttpClient());
        return (paths, environment, runtimeProfileService, registry, runtime);
    }

    private static JsonObject CreateTool(
        string name,
        string description,
        bool readOnlyHint = false,
        bool destructiveHint = false) =>
        new()
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject { ["type"] = "string" }
                }
            },
            ["annotations"] = new JsonObject
            {
                ["readOnlyHint"] = readOnlyHint,
                ["destructiveHint"] = destructiveHint
            }
        };

    private sealed class FakeMcpHttpServer : IAsyncDisposable
    {
        private readonly HttpListener listener;
        private readonly Task loopTask;
        private readonly IReadOnlyList<JsonObject> tools;
        private readonly Func<string, JsonObject?, JsonObject> callHandler;

        private FakeMcpHttpServer(
            string url,
            IReadOnlyList<JsonObject> tools,
            Func<string, JsonObject?, JsonObject> callHandler)
        {
            Url = url;
            this.tools = tools;
            this.callHandler = callHandler;
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            loopTask = Task.Run(ServeAsync);
        }

        public string Url { get; }

        public static Task<FakeMcpHttpServer> StartAsync(
            IReadOnlyList<JsonObject> tools,
            Func<string, JsonObject?, JsonObject>? callHandler = null)
        {
            var port = GetFreePort();
            var url = $"http://127.0.0.1:{port}/";
            Func<string, JsonObject?, JsonObject> effectiveHandler = callHandler ??
                ((toolName, _) =>
                    new JsonObject
                    {
                        ["content"] = new JsonArray(new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = $"called {toolName}"
                        }),
                        ["isError"] = false
                    });
            return Task.FromResult(
                new FakeMcpHttpServer(
                    url,
                    tools,
                    effectiveHandler));
        }

        public async ValueTask DisposeAsync()
        {
            listener.Stop();
            listener.Close();

            try
            {
                await loopTask;
            }
            catch
            {
            }
        }

        private async Task ServeAsync()
        {
            while (listener.IsListening)
            {
                HttpListenerContext? context = null;

                try
                {
                    context = await listener.GetContextAsync();
                }
                catch
                {
                    break;
                }

                if (context is null)
                {
                    continue;
                }

                await HandleAsync(context);
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            JsonObject request;
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                var body = await reader.ReadToEndAsync();
                request = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body) as JsonObject ?? [];
            }

            var method = request["method"]?.GetValue<string?>() ?? string.Empty;
            var idNode = request["id"];
            JsonObject? response = method switch
            {
                "initialize" => CreateResponse(idNode, new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject()
                    },
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "fake-mcp",
                        ["version"] = "1.0.0"
                    }
                }),
                "tools/list" => CreateResponse(idNode, new JsonObject
                {
                    ["tools"] = new JsonArray(tools.Select(tool => JsonNode.Parse(tool.ToJsonString())!).ToArray())
                }),
                "tools/call" => CreateResponse(
                    idNode,
                    callHandler(
                        request["params"]?["name"]?.GetValue<string?>() ?? "unknown",
                        request["params"]?["arguments"] as JsonObject)),
                _ when idNode is null => null,
                _ => CreateErrorResponse(idNode, $"Unsupported MCP method '{method}'.")
            };

            if (response is null)
            {
                context.Response.StatusCode = 202;
                context.Response.Close();
                return;
            }

            var payload = Encoding.UTF8.GetBytes(response.ToJsonString());
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload);
            context.Response.Close();
        }

        private static JsonObject CreateResponse(JsonNode? idNode, JsonObject result) =>
            new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = idNode?.DeepClone(),
                ["result"] = result
            };

        private static JsonObject CreateErrorResponse(JsonNode? idNode, string message) =>
            new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = idNode?.DeepClone(),
                ["error"] = new JsonObject
                {
                    ["code"] = -32601,
                    ["message"] = message
                }
            };

        private static int GetFreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}
