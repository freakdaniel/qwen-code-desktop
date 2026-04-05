using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using QwenCode.Tests.Shared.Fakes;

namespace QwenCode.Tests.Tools;

public sealed class WebToolServiceTests
{
    [Fact]
    public async Task WebToolService_FetchAsync_ExtractsReadableHtmlForPrompt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-web-fetch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);

            var runtimeProfile = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, null))
                .Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            var handler = new StubHttpMessageHandler(request =>
            {
                Assert.Equal("https://example.com/article", request.RequestUri!.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "<html><body><h1>Build report</h1><p>The deployment succeeded and latency improved.</p><p>Unrelated footer.</p></body></html>",
                        Encoding.UTF8,
                        "text/html")
                };
            });

            var service = new WebToolService(
                new FakeDesktopEnvironmentPaths(homeRoot, null),
                new HttpClient(handler));

            using var document = JsonDocument.Parse("""{"url":"https://example.com/article","prompt":"deployment latency"}""");
            var output = await service.FetchAsync(runtimeProfile, document.RootElement);

            Assert.Contains("Fetched content from https://example.com/article", output);
            Assert.Contains("deployment latency", output);
            Assert.Contains("deployment succeeded and latency improved", output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WebToolService_SearchAsync_UsesConfiguredTavilyProviderAndFormatsSources()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-web-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "webSearch": {
                    "provider": [
                      {
                        "type": "tavily",
                        "apiKey": "test-key",
                        "maxResults": 3,
                        "includeAnswer": true
                      }
                    ],
                    "default": "tavily"
                  }
                }
                """);

            var runtimeProfile = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, null))
                .Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            var handler = new StubHttpMessageHandler(async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://api.tavily.com/search", request.RequestUri!.ToString());
                var requestBody = await request.Content!.ReadAsStringAsync();
                Assert.Contains("\"query\":\"qwen code desktop\"", requestBody);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "answer":"Qwen Code Desktop continues the C# port with native tooling.",
                          "results":[
                            {
                              "title":"Port status",
                              "url":"https://example.com/port",
                              "content":"Desktop-native qwen runtime progress",
                              "score":0.95,
                              "published_date":"2026-04-02"
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            });

            var service = new WebToolService(
                new FakeDesktopEnvironmentPaths(homeRoot, null),
                new HttpClient(handler));

            using var document = JsonDocument.Parse("""{"query":"qwen code desktop"}""");
            var output = await service.SearchAsync(runtimeProfile, document.RootElement);

            Assert.Contains("Web search results for \"qwen code desktop\" (via tavily):", output);
            Assert.Contains("Qwen Code Desktop continues the C# port with native tooling.", output);
            Assert.Contains("Sources:", output);
            Assert.Contains("[1] Port status (https://example.com/port)", output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_HandlesWebFetchAndWebSearch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-web-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "yolo"
                  },
                  "webSearch": {
                    "provider": [
                      {
                        "type": "google",
                        "apiKey": "google-key",
                        "searchEngineId": "cse-id",
                        "maxResults": 2
                      }
                    ],
                    "default": "google"
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var handler = new StubHttpMessageHandler(request =>
            {
                if (request.RequestUri!.Host.Equals("example.com", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("<html><body><p>Qwen desktop article body</p></body></html>", Encoding.UTF8, "text/html")
                    };
                }

                if (request.RequestUri.Host.Equals("www.googleapis.com", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """
                            {
                              "items":[
                                {
                                  "title":"Search result",
                                  "link":"https://example.com/search",
                                  "snippet":"Snippet from search"
                                }
                              ]
                            }
                            """,
                            Encoding.UTF8,
                            "application/json")
                    };
                }

                throw new InvalidOperationException($"Unexpected request URI {request.RequestUri}");
            });

            var webService = new WebToolService(environmentPaths, new HttpClient(handler));
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                new InMemoryCronScheduler(),
                webService);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };

            var fetchResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "web_fetch",
                ArgumentsJson = """{"url":"https://example.com/page","prompt":"article body"}""",
                ApproveExecution = true
            });

            var searchResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "web_search",
                ArgumentsJson = """{"query":"desktop port"}""",
                ApproveExecution = true
            });

            Assert.Equal("completed", fetchResult.Status);
            Assert.Contains("Qwen desktop article body", fetchResult.Output);
            Assert.Equal("completed", searchResult.Status);
            Assert.Contains("Search result", searchResult.Output);
            Assert.Contains("Snippet from search", searchResult.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> asyncResponder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            asyncResponder = request => Task.FromResult(responder(request));
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            asyncResponder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            asyncResponder(request);
    }
}
