using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public sealed class IdeCompanionTransport(HttpClient httpClient) : IIdeCompanionTransport
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SemaphoreSlim gate = new(1, 1);
    private IIdeTransportSession? session;

    public async Task<bool> ConnectAsync(IdeTransportConnectionInfo connection, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (session is not null)
            {
                await session.DisposeAsync();
                session = null;
            }

            if (!string.IsNullOrWhiteSpace(connection.Port))
            {
                foreach (var host in BuildCandidateHosts())
                {
                    var httpSession = new HttpIdeTransportSession(httpClient, host, connection.Port, connection.AuthToken);
                    try
                    {
                        await httpSession.InitializeAsync(cancellationToken);
                        session = httpSession;
                        return true;
                    }
                    catch
                    {
                        await httpSession.DisposeAsync();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(connection.StdioCommand))
            {
                var stdioSession = new StdioIdeTransportSession(connection);
                try
                {
                    await stdioSession.InitializeAsync(cancellationToken);
                    session = stdioSession;
                    return true;
                }
                catch
                {
                    await stdioSession.DisposeAsync();
                }
            }

            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
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
        }
    }

    public async Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return session is null
                ? []
                : await session.ListToolsAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IdeToolCallResult> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return session is null
                ? new IdeToolCallResult { IsError = true, Text = "IDE companion transport is not connected." }
                : await session.CallToolAsync(toolName, arguments, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static IReadOnlyList<string> BuildCandidateHosts() =>
        ["127.0.0.1", "host.docker.internal"];

    private static IReadOnlyList<string> ParseToolNames(JsonArray? tools) =>
        tools?
            .OfType<JsonObject>()
            .Select(static tool => tool["name"]?.GetValue<string?>())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private static string FormatToolResult(JsonArray? content)
    {
        if (content is null || content.Count == 0)
        {
            return string.Empty;
        }

        var parts = content
            .OfType<JsonObject>()
            .Select(static item => item["type"]?.GetValue<string?>() switch
            {
                "text" => item["text"]?.GetValue<string?>() ?? string.Empty,
                _ => item.ToJsonString(SerializerOptions)
            })
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        return string.Join(Environment.NewLine, parts);
    }

    private static JsonObject ParseResult(string payload, string method)
    {
        var response = JsonNode.Parse(payload) as JsonObject
            ?? throw new InvalidOperationException("IDE companion returned an invalid JSON-RPC response.");

        if (response["error"] is JsonObject error)
        {
            throw new InvalidOperationException(
                error["message"]?.GetValue<string?>() ?? $"IDE companion returned an error for '{method}'.");
        }

        return response["result"] as JsonObject
            ?? throw new InvalidOperationException($"IDE companion did not return a JSON-RPC result for '{method}'.");
    }

    private interface IIdeTransportSession : IAsyncDisposable
    {
        Task InitializeAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken);

        Task<IdeToolCallResult> CallToolAsync(
            string toolName,
            JsonObject arguments,
            CancellationToken cancellationToken);
    }

    private sealed class HttpIdeTransportSession(
        HttpClient httpClient,
        string host,
        string port,
        string authToken) : IIdeTransportSession
    {
        private readonly Uri endpoint = new($"http://{host}:{port}/mcp", UriKind.Absolute);
        private int nextId = 1;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await SendRequestAsync(
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

        public async Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            return ParseToolNames(result["tools"] as JsonArray);
        }

        public async Task<IdeToolCallResult> CallToolAsync(
            string toolName,
            JsonObject arguments,
            CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync(
                "tools/call",
                new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments
                },
                cancellationToken);

            return new IdeToolCallResult
            {
                IsError = result["isError"]?.GetValue<bool?>() ?? false,
                Text = FormatToolResult(result["content"] as JsonArray)
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private async Task<JsonObject> SendRequestAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = nextId++,
                ["method"] = method,
                ["params"] = parameters
            };

            using var request = CreateRequest(payload);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResult(content, method);
        }

        private async Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            var payload = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = parameters
            };

            using var request = CreateRequest(payload);
            using var _ = await httpClient.SendAsync(request, cancellationToken);
        }

        private HttpRequestMessage CreateRequest(JsonObject payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload.ToJsonString(SerializerOptions), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
            }

            return request;
        }
    }

    private sealed class StdioIdeTransportSession : IIdeTransportSession
    {
        private readonly Process process;
        private readonly StreamWriter stdin;
        private readonly StreamReader stdout;
        private readonly StringBuilder stderr = new();
        private bool disposed;
        private int nextId = 1;

        public StdioIdeTransportSession(IdeTransportConnectionInfo connection)
        {
            process = StartProcess(connection);
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
            await SendRequestAsync(
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

        public async Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync("tools/list", new JsonObject(), cancellationToken);
            return ParseToolNames(result["tools"] as JsonArray);
        }

        public async Task<IdeToolCallResult> CallToolAsync(
            string toolName,
            JsonObject arguments,
            CancellationToken cancellationToken)
        {
            var result = await SendRequestAsync(
                "tools/call",
                new JsonObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments
                },
                cancellationToken);

            return new IdeToolCallResult
            {
                IsError = result["isError"]?.GetValue<bool?>() ?? false,
                Text = FormatToolResult(result["content"] as JsonArray)
            };
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

        private async Task<JsonObject> SendRequestAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));

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
                    ?? throw new InvalidOperationException("IDE companion returned invalid JSON.");

                if (response["id"]?.GetValue<int?>() != id)
                {
                    continue;
                }

                if (response["error"] is JsonObject error)
                {
                    var message = error["message"]?.GetValue<string?>()
                        ?? $"IDE companion returned an error for '{method}'.";
                    var stderrText = GetStderrText();
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(stderrText) ? message : $"{message} {stderrText}".Trim());
                }

                return response["result"] as JsonObject
                    ?? throw new InvalidOperationException($"IDE companion did not return a JSON-RPC result for '{method}'.");
            }

            throw new OperationCanceledException(timeoutSource.Token);
        }

        private async Task SendNotificationAsync(string method, JsonObject parameters, CancellationToken cancellationToken)
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

        private static Process StartProcess(IdeTransportConnectionInfo connection)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = connection.StdioCommand,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(connection.WorkspacePath)
                    ? Environment.CurrentDirectory
                    : connection.WorkspacePath
            };

            foreach (var argument in connection.StdioArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();
            return process;
        }
    }
}
