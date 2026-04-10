using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.Core.Ide;
using QwenCode.Core.Models;

namespace QwenCode.Tests.Ide;

public sealed class IdeCompanionTransportTests
{
    [Fact]
    public async Task IdeCompanionTransport_ConnectsOverHttp_AndListsTools()
    {
        var handler = new FakeHttpMessageHandler();
        var transport = new IdeCompanionTransport(new HttpClient(handler));
        var workspacePath = Path.Combine(Path.GetTempPath(), "qwen-ide-transport-workspace");

        var connected = await transport.ConnectAsync(new IdeTransportConnectionInfo
        {
            WorkspacePath = workspacePath,
            Port = "4111",
            AuthToken = "secret-token"
        });

        Assert.True(connected);

        var tools = await transport.ListToolsAsync();

        Assert.Contains("openDiff", tools);
        Assert.Contains("closeDiff", tools);
        Assert.Contains("Authorization:Bearersecret-token", handler.ObservedHeaders);
    }

    [Fact]
    public async Task IdeCompanionTransport_CallToolAsync_FormatsTextBlocks()
    {
        var handler = new FakeHttpMessageHandler();
        var transport = new IdeCompanionTransport(new HttpClient(handler));
        var workspacePath = Path.Combine(Path.GetTempPath(), "qwen-ide-transport-workspace");
        await transport.ConnectAsync(new IdeTransportConnectionInfo
        {
            WorkspacePath = workspacePath,
            Port = "4111"
        });

        var result = await transport.CallToolAsync(
            "closeDiff",
            new JsonObject
            {
                ["filePath"] = Path.Combine(workspacePath, "test.cs")
            });

        Assert.False(result.IsError);
        Assert.Equal("""{"content":"patched"}""", result.Text);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public List<string> ObservedHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is not null)
            {
                ObservedHeaders.Add($"Authorization:{request.Headers.Authorization.Scheme}{request.Headers.Authorization.Parameter}");
            }

            var payload = request.Content is null
                ? "{}"
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var method = root.GetProperty("method").GetString();

            var responsePayload = method switch
            {
                "initialize" => JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = root.GetProperty("id").GetInt32(),
                    result = new
                    {
                        capabilities = new { }
                    }
                }),
                "notifications/initialized" => JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    result = new { }
                }),
                "tools/list" => JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = root.GetProperty("id").GetInt32(),
                    result = new
                    {
                        tools = new object[]
                        {
                            new { name = "openDiff" },
                            new { name = "closeDiff" }
                        }
                    }
                }),
                "tools/call" => JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = root.GetProperty("id").GetInt32(),
                    result = new
                    {
                        isError = false,
                        content = new object[]
                        {
                            new { type = "text", text = """{"content":"patched"}""" }
                        }
                    }
                }),
                _ => JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = root.TryGetProperty("id", out var idElement) ? idElement.GetInt32() : 0,
                    error = new
                    {
                        message = $"Unexpected method '{method}'."
                    }
                })
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload, Encoding.UTF8, "application/json")
            });
        }
    }
}
