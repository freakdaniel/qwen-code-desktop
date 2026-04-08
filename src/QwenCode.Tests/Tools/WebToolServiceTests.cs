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
                Assert.Equal("https://example.com/build-report", request.RequestUri!.ToString());
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

            using var document = JsonDocument.Parse("""{"url":"https://example.com/build-report","prompt":"deployment latency"}""");
            var output = await service.FetchAsync(runtimeProfile, document.RootElement);

            Assert.Contains("Fetched content from https://example.com/build-report", output);
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
    public async Task WebToolService_SearchAsync_ReadsUserSettingsWithCommentsAndTrailingCommas()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-web-search-user-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));

            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """
                {
                  // user-scoped web search provider
                  "webSearch": {
                    "provider": [
                      {
                        "type": "tavily",
                        "apiKey": "user-test-key",
                        "maxResults": 2,
                      },
                    ],
                    "default": "tavily",
                  },
                }
                """);

            var runtimeProfile = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, null))
                .Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            var handler = new StubHttpMessageHandler(async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://api.tavily.com/search", request.RequestUri!.ToString());
                var requestBody = await request.Content!.ReadAsStringAsync();
                Assert.Contains("\"query\":\"moscow weather tomorrow\"", requestBody);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "results":[
                            {
                              "title":"Forecast",
                              "url":"https://example.com/weather",
                              "content":"Snow is possible overnight."
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

            using var document = JsonDocument.Parse("""{"query":"moscow weather tomorrow"}""");
            var output = await service.SearchAsync(runtimeProfile, document.RootElement);

            Assert.Contains("Web search results for \"moscow weather tomorrow\" (via tavily):", output);
            Assert.Contains("Forecast", output);
            Assert.DoesNotContain("Web search is disabled", output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WebToolService_FetchAsync_FollowsPermittedSameHostRedirect()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-web-fetch-redirect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);

            var runtimeProfile = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, null))
                .Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            var requestCount = 0;
            var handler = new StubHttpMessageHandler(request =>
            {
                requestCount++;

                if (requestCount == 1)
                {
                    Assert.Equal("https://example.com/permitted-redirect", request.RequestUri!.ToString());
                    var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                    redirect.Headers.Location = new Uri("https://www.example.com/permitted-redirect");
                    return redirect;
                }

                Assert.Equal("https://www.example.com/permitted-redirect", request.RequestUri!.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "<html><body><p>Latency improved after deployment.</p></body></html>",
                        Encoding.UTF8,
                        "text/html")
                };
            });

            var service = new WebToolService(
                new FakeDesktopEnvironmentPaths(homeRoot, null),
                new HttpClient(handler));

            using var document = JsonDocument.Parse("""{"url":"https://example.com/permitted-redirect","prompt":"latency"}""");
            var output = await service.FetchAsync(runtimeProfile, document.RootElement);

            Assert.Equal(2, requestCount);
            Assert.Contains("Fetched content from https://www.example.com/permitted-redirect", output);
            Assert.Contains("Latency improved after deployment", output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task WebToolService_FetchAsync_ReturnsRedirectInstructionsForCrossHostRedirect()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-web-fetch-cross-redirect-{Guid.NewGuid():N}");
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
                Assert.Equal("https://example.com/cross-host-redirect", request.RequestUri!.ToString());
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://news.example.net/cross-host-redirect");
                return redirect;
            });

            var service = new WebToolService(
                new FakeDesktopEnvironmentPaths(homeRoot, null),
                new HttpClient(handler));

            using var document = JsonDocument.Parse("""{"url":"https://example.com/cross-host-redirect","prompt":"summary"}""");
            var output = await service.FetchAsync(runtimeProfile, document.RootElement);

            Assert.Contains("redirected to a different host", output);
            Assert.Contains("Original URL: https://example.com/cross-host-redirect", output);
            Assert.Contains("Redirect URL: https://news.example.net/cross-host-redirect", output);
            Assert.Contains("Prompt: summary", output);
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
