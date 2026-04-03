using System.Net;
using System.Net.Sockets;
using System.Text;

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
            ],
            prompts:
            [
                CreatePrompt("workspace-summary", "Summarizes the workspace")
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
            Assert.Contains("Prompts:", output);
            Assert.Contains("docs/workspace-summary", output);
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

    [Fact]
    public async Task McpToolRuntimeService_ConnectServerAsync_SseTransport_DiscoversTools()
    {
        await using var server = await FakeMcpSseServer.StartAsync(
            [CreateTool("sse-docs", "Streams docs", readOnlyHint: true)]);

        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-sse-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (paths, _, _, registry, runtime) = CreateRuntime(root);
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "stream-docs",
                Scope = "project",
                Transport = "sse",
                CommandOrUrl = server.StreamUrl
            });

            var result = await runtime.ConnectServerAsync(paths, "stream-docs");
            var description = await runtime.DescribeAsync(paths, JsonDocument.Parse("""{"server_name":"stream-docs"}""").RootElement);

            Assert.Equal("connected", result.Status);
            Assert.Equal(1, result.DiscoveredToolsCount);
            Assert.Contains("mcp__stream-docs__sse-docs [read-only]", description);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task McpToolRuntimeService_ReadResourceAsync_ReturnsResourceContents()
    {
        await using var server = await FakeMcpHttpServer.StartAsync(
            [CreateTool("read-doc", "Reads docs", readOnlyHint: true)]);

        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-resource-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (paths, _, _, registry, runtime) = CreateRuntime(root);
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "docs",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = server.Url,
                Trust = true
            });

            var result = await runtime.ReadResourceAsync(paths, "docs", "file://README.md");

            Assert.Equal("docs", result.ServerName);
            Assert.Equal("file://README.md", result.Uri);
            Assert.Contains("resource content for file://README.md", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_McpClientPromptInvocation_ReturnsPromptMessages()
    {
        await using var server = await FakeMcpHttpServer.StartAsync(
            [CreateTool("read-doc", "Reads docs", readOnlyHint: true)],
            prompts:
            [
                CreatePrompt("workspace-summary", "Summarizes the workspace")
            ],
            promptHandler: static (promptName, arguments) =>
                new JsonObject
                {
                    ["messages"] = new JsonArray(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = $"prompt {promptName}: {arguments?["scope"]?.GetValue<string?>() ?? "default"}"
                        }
                    })
                });

        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-prompt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (paths, _, runtimeProfileService, registry, runtime) = CreateRuntime(root);
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
                ToolName = "mcp-client",
                ArgumentsJson = """{"server_name":"docs","prompt_name":"workspace-summary","arguments":{"scope":"repo"}}"""
            });

            Assert.Equal("completed", result.Status);
            Assert.Contains("prompt workspace-summary: repo", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_McpClientResourceRead_DeniesUntrustedServer()
    {
        await using var server = await FakeMcpHttpServer.StartAsync(
            [CreateTool("read-doc", "Reads docs", readOnlyHint: true)]);

        var root = Path.Combine(Path.GetTempPath(), $"qwen-mcp-resource-trust-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var (paths, _, runtimeProfileService, registry, runtime) = CreateRuntime(root);
            registry.AddServer(paths, new McpServerRegistrationRequest
            {
                Name = "docs",
                Scope = "project",
                Transport = "http",
                CommandOrUrl = server.Url,
                Trust = false
            });

            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                new InMemoryCronScheduler(),
                mcpToolRuntime: runtime);

            var result = await host.ExecuteAsync(paths, new ExecuteNativeToolRequest
            {
                ToolName = "mcp-client",
                ArgumentsJson = """{"server_name":"docs","uri":"file://README.md"}"""
            });

            Assert.Equal("error", result.Status);
            Assert.Contains("untrusted servers", result.ErrorMessage);
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

    private static JsonObject CreatePrompt(string name, string description) =>
        new()
        {
            ["name"] = name,
            ["description"] = description,
            ["arguments"] = new JsonArray(new JsonObject
            {
                ["name"] = "scope",
                ["description"] = "Scope selector",
                ["required"] = false
            })
        };

    private sealed class FakeMcpHttpServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource shutdown = new();
        private readonly Task loopTask;
        private readonly IReadOnlyList<JsonObject> tools;
        private readonly Func<string, JsonObject?, JsonObject> callHandler;
        private readonly IReadOnlyList<JsonObject> prompts;
        private readonly Func<string, JsonObject?, JsonObject> promptHandler;

        private FakeMcpHttpServer(
            string url,
            IReadOnlyList<JsonObject> tools,
            Func<string, JsonObject?, JsonObject> callHandler,
            IReadOnlyList<JsonObject> prompts,
            Func<string, JsonObject?, JsonObject> promptHandler)
        {
            Url = url;
            this.tools = tools;
            this.callHandler = callHandler;
            this.prompts = prompts;
            this.promptHandler = promptHandler;
            var uri = new Uri(url, UriKind.Absolute);
            listener = new TcpListener(IPAddress.Parse(uri.Host), uri.Port);
            listener.Start();
            loopTask = Task.Run(ServeAsync);
        }

        public string Url { get; }

        public static Task<FakeMcpHttpServer> StartAsync(
            IReadOnlyList<JsonObject> tools,
            Func<string, JsonObject?, JsonObject>? callHandler = null,
            IReadOnlyList<JsonObject>? prompts = null,
            Func<string, JsonObject?, JsonObject>? promptHandler = null)
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
            Func<string, JsonObject?, JsonObject> effectivePromptHandler = promptHandler ??
                ((promptName, arguments) =>
                    new JsonObject
                    {
                        ["messages"] = new JsonArray(new JsonObject
                        {
                            ["role"] = "assistant",
                            ["content"] = new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = $"prompt {promptName}: {arguments?["scope"]?.GetValue<string?>() ?? "default"}"
                            }
                        })
                    });
            return Task.FromResult(
                new FakeMcpHttpServer(
                    url,
                    tools,
                    effectiveHandler,
                    prompts ?? [],
                    effectivePromptHandler));
        }

        public async ValueTask DisposeAsync()
        {
            await shutdown.CancelAsync();
            listener.Stop();

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
            while (!shutdown.IsCancellationRequested)
            {
                TcpClient? client = null;

                try
                {
                    client = await listener.AcceptTcpClientAsync(shutdown.Token);
                }
                catch
                {
                    break;
                }

                if (client is null)
                {
                    continue;
                }

                _ = Task.Run(() => HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using var clientScope = client;
            var stream = client.GetStream();
            var (_, _, body) = await ReadHttpRequestAsync(stream, shutdown.Token);

            JsonObject request;
            using (var reader = new StringReader(body))
            {
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
                        ["tools"] = new JsonObject(),
                        ["prompts"] = new JsonObject(),
                        ["resources"] = new JsonObject()
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
                "prompts/list" => CreateResponse(idNode, new JsonObject
                {
                    ["prompts"] = new JsonArray(prompts.Select(prompt => JsonNode.Parse(prompt.ToJsonString())!).ToArray())
                }),
                "prompts/get" => CreateResponse(
                    idNode,
                    promptHandler(
                        request["params"]?["name"]?.GetValue<string?>() ?? "unknown",
                        request["params"]?["arguments"] as JsonObject)),
                "tools/call" => CreateResponse(
                    idNode,
                    callHandler(
                        request["params"]?["name"]?.GetValue<string?>() ?? "unknown",
                        request["params"]?["arguments"] as JsonObject)),
                "resources/read" => CreateResponse(idNode, new JsonObject
                {
                    ["contents"] = new JsonArray(new JsonObject
                    {
                        ["uri"] = request["params"]?["uri"]?.GetValue<string?>() ?? "unknown",
                        ["mimeType"] = "text/plain",
                        ["text"] = $"resource content for {request["params"]?["uri"]?.GetValue<string?>() ?? "unknown"}"
                    })
                }),
                _ when idNode is null => null,
                _ => CreateErrorResponse(idNode, $"Unsupported MCP method '{method}'.")
            };

            if (response is null)
            {
                await WriteHttpResponseAsync(stream, 202, "application/json", string.Empty, shutdown.Token);
                return;
            }

            await WriteHttpResponseAsync(
                stream,
                200,
                "application/json",
                response.ToJsonString(),
                shutdown.Token);
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

    private sealed class FakeMcpSseServer : IAsyncDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource shutdown = new();
        private readonly Task loopTask;
        private readonly IReadOnlyList<JsonObject> tools;
        private readonly SemaphoreSlim streamReady = new(0, 1);
        private TcpClient? streamClient;
        private StreamWriter? streamWriter;

        private FakeMcpSseServer(string baseUrl, IReadOnlyList<JsonObject> tools)
        {
            BaseUrl = baseUrl;
            StreamUrl = baseUrl;
            MessageUrl = $"{baseUrl}messages/";
            this.tools = tools;
            var uri = new Uri(baseUrl, UriKind.Absolute);
            listener = new TcpListener(IPAddress.Parse(uri.Host), uri.Port);
            listener.Start();
            loopTask = Task.Run(ServeAsync);
        }

        public string BaseUrl { get; }

        public string StreamUrl { get; }

        public string MessageUrl { get; }

        public static Task<FakeMcpSseServer> StartAsync(IReadOnlyList<JsonObject> tools)
        {
            var port = GetFreePort();
            var url = $"http://127.0.0.1:{port}/";
            return Task.FromResult(new FakeMcpSseServer(url, tools));
        }

        public async ValueTask DisposeAsync()
        {
            await shutdown.CancelAsync();

            try
            {
                if (streamWriter is not null)
                {
                    await streamWriter.DisposeAsync();
                }
            }
            catch
            {
            }

            try
            {
                streamClient?.Close();
            }
            catch
            {
            }

            listener.Stop();

            try
            {
                await loopTask;
            }
            catch
            {
            }

            streamReady.Dispose();
        }

        private async Task ServeAsync()
        {
            while (!shutdown.IsCancellationRequested)
            {
                TcpClient? client = null;

                try
                {
                    client = await listener.AcceptTcpClientAsync(shutdown.Token);
                }
                catch
                {
                    break;
                }

                if (client is null)
                {
                    continue;
                }

                _ = Task.Run(() => HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            var stream = client.GetStream();
            var (method, _, body) = await ReadHttpRequestAsync(stream, shutdown.Token);

            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await HandleStreamAsync(client, stream);
                return;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandleMessageAsync(stream, body);
                client.Close();
                return;
            }

            await WriteHttpResponseAsync(stream, 405, "text/plain", string.Empty, shutdown.Token);
            client.Close();
        }

        private async Task HandleStreamAsync(TcpClient client, NetworkStream stream)
        {
            streamClient = client;
            streamWriter = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };

            await streamWriter.WriteAsync(
                "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nCache-Control: no-cache\r\nConnection: keep-alive\r\n\r\n");
            await streamWriter.WriteAsync($"event: endpoint\n");
            await streamWriter.WriteAsync($"data: {MessageUrl}\n\n");
            streamReady.Release();

            try
            {
                await Task.Delay(Timeout.Infinite, shutdown.Token);
            }
            catch
            {
            }
        }

        private async Task HandleMessageAsync(NetworkStream stream, string body)
        {
            await streamReady.WaitAsync();
            streamReady.Release();

            JsonObject request;
            using (var reader = new StringReader(body))
            {
                request = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body) as JsonObject ?? [];
            }

            await WriteHttpResponseAsync(stream, 202, "text/plain", string.Empty, shutdown.Token);

            var method = request["method"]?.GetValue<string?>() ?? string.Empty;
            var idNode = request["id"];

            if (idNode is null || streamWriter is null)
            {
                return;
            }

            JsonObject response = method switch
            {
                "initialize" => CreateResponse(idNode, new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject(),
                        ["resources"] = new JsonObject()
                    },
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "fake-sse-mcp",
                        ["version"] = "1.0.0"
                    }
                }),
                "tools/list" => CreateResponse(idNode, new JsonObject
                {
                    ["tools"] = new JsonArray(tools.Select(tool => JsonNode.Parse(tool.ToJsonString())!).ToArray())
                }),
                "resources/read" => CreateResponse(idNode, new JsonObject
                {
                    ["contents"] = new JsonArray(new JsonObject
                    {
                        ["uri"] = request["params"]?["uri"]?.GetValue<string?>() ?? "unknown",
                        ["mimeType"] = "text/plain",
                        ["text"] = $"resource content for {request["params"]?["uri"]?.GetValue<string?>() ?? "unknown"}"
                    })
                }),
                _ => CreateErrorResponse(idNode, $"Unsupported MCP method '{method}'.")
            };

            await streamWriter.WriteAsync($"event: message\n");
            await streamWriter.WriteAsync($"data: {response.ToJsonString()}\n\n");
            await streamWriter.FlushAsync();
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

    private static async Task<(string Method, string Path, string Body)> ReadHttpRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var received = new MemoryStream();
        var headerTerminator = Encoding.ASCII.GetBytes("\r\n\r\n");

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await received.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (IndexOfSequence(received.GetBuffer().AsSpan(0, (int)received.Length), headerTerminator) >= 0)
            {
                break;
            }
        }

        var requestBytes = received.ToArray();
        var requestText = Encoding.UTF8.GetString(requestBytes);
        var headerEnd = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (headerEnd < 0)
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var headerText = requestText[..headerEnd];
        var lines = headerText.Split("\r\n");
        var requestLine = lines[0].Split(' ');
        var method = requestLine.ElementAtOrDefault(0) ?? string.Empty;
        var path = requestLine.ElementAtOrDefault(1) ?? "/";

        var contentLength = 0;
        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            if (string.Equals(line[..separator], "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line[(separator + 1)..].Trim(), out contentLength);
            }
        }

        var bodyOffset = headerEnd + 4;
        var bodyBytes = requestBytes.AsSpan(bodyOffset).ToArray();

        while (bodyBytes.Length < contentLength)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            bodyBytes = [.. bodyBytes, .. buffer.AsSpan(0, read).ToArray()];
        }

        return (method, path, Encoding.UTF8.GetString(bodyBytes, 0, Math.Min(contentLength, bodyBytes.Length)));
    }

    private static async Task WriteHttpResponseAsync(
        NetworkStream stream,
        int statusCode,
        string contentType,
        string body,
        CancellationToken cancellationToken)
    {
        var reasonPhrase = statusCode switch
        {
            200 => "OK",
            202 => "Accepted",
            405 => "Method Not Allowed",
            _ => "OK"
        };
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var header =
            $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n" +
            "Connection: close\r\n\r\n";

        var headerBytes = Encoding.UTF8.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken);
        if (bodyBytes.Length > 0)
        {
            await stream.WriteAsync(bodyBytes, cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
    }

    private static int IndexOfSequence(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> sequence)
    {
        for (var i = 0; i <= buffer.Length - sequence.Length; i++)
        {
            if (buffer.Slice(i, sequence.Length).SequenceEqual(sequence))
            {
                return i;
            }
        }

        return -1;
    }
}
