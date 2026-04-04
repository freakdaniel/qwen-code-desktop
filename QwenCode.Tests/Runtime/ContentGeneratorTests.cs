using System.Net.Http.Headers;
using QwenCode.App.Config;

namespace QwenCode.Tests.Runtime;

public sealed class ContentGeneratorTests
{
    [Fact]
    public async Task OpenAiCompatibleBaseLlmClient_GenerateJsonAsync_ParsesJsonObjectFromAssistantContent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-content-json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "openai"
                    }
                  },
                  "env": {
                    "OPENAI_API_KEY": "json-settings-key"
                  },
                  "model": {
                    "name": "qwen3-coder-plus"
                  }
                }
                """);

            HttpRequestMessage? capturedRequest = null;
            string? capturedPayload = null;
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                capturedRequest = request;
                capturedPayload = request.Content is null
                    ? null
                    : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """
                            {
                              "choices": [
                                {
                                  "message": {
                                    "content": "{\"next\":\"run the tests\",\"confidence\":0.9}"
                                  }
                                }
                              ]
                            }
                            """,
                            Encoding.UTF8,
                            "application/json")
                    });
            }));

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var configService = new RuntimeConfigService(environmentPaths);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var client = new OpenAiCompatibleBaseLlmClient(
                httpClient,
                new ProviderConfigurationResolver(
                    environmentPaths,
                    configService: configService,
                    modelConfigResolver: new ModelConfigResolver(new ModelRegistryService(configService))),
                new ModelConfigResolver(new ModelRegistryService(configService)),
                Options.Create(new NativeAssistantRuntimeOptions
                {
                    Provider = "openai-compatible",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY"
                }));

            var result = await client.GenerateJsonAsync(
                new JsonGenerationRequest
                {
                    ContentRequest = new LlmContentRequest
                    {
                        SessionId = "json-session",
                        Prompt = "Return a JSON object with the next step.",
                        WorkingDirectory = workspaceRoot,
                        TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "json-session.jsonl"),
                        RuntimeProfile = runtimeProfile,
                        PromptContext = new AssistantPromptContext
                        {
                            SessionSummary = "Transcript messages loaded: 0",
                            HistoryHighlights = [],
                            ContextFiles = [],
                            Messages = []
                        },
                        SystemPrompt = "Return only JSON.",
                        DisableTools = true
                    }
                });

            Assert.NotNull(result);
            Assert.Equal("run the tests", result!["next"]?.GetValue<string>());
            Assert.Equal(0.9, result["confidence"]?.GetValue<double>());
            Assert.NotNull(capturedRequest);
            Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", capturedRequest!.RequestUri!.ToString());
            Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
            Assert.Equal("json-settings-key", capturedRequest.Headers.Authorization?.Parameter);

            var payload = JsonNode.Parse(capturedPayload!)!.AsObject();
            Assert.Equal("none", payload["tool_choice"]?.GetValue<string>());
            Assert.False(payload.AsObject().ContainsKey("tools"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenAiCompatibleBaseLlmClient_GenerateEmbeddingAsync_PostsToEmbeddingsEndpoint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-content-embedding-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "openai"
                    }
                  },
                  "env": {
                    "OPENAI_API_KEY": "embedding-settings-key"
                  },
                  "model": {
                    "name": "qwen3-coder-plus"
                  },
                  "embeddingModel": "text-embedding-v4"
                }
                """);

            HttpRequestMessage? capturedRequest = null;
            string? capturedPayload = null;
            var httpClient = new HttpClient(new RecordingHttpMessageHandler((request, _) =>
            {
                capturedRequest = request;
                capturedPayload = request.Content is null
                    ? null
                    : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "data": [
                            {
                              "embedding": [0.1, 0.2, 0.3]
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            }));

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var configService = new RuntimeConfigService(environmentPaths);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var client = new OpenAiCompatibleBaseLlmClient(
                httpClient,
                new ProviderConfigurationResolver(
                    environmentPaths,
                    configService: configService,
                    modelConfigResolver: new ModelConfigResolver(new ModelRegistryService(configService))),
                new ModelConfigResolver(new ModelRegistryService(configService)),
                Options.Create(new NativeAssistantRuntimeOptions
                {
                    Provider = "openai-compatible",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY"
                }));

            var result = await client.GenerateEmbeddingAsync(
                new EmbeddingGenerationRequest
                {
                    SessionId = "embedding-session",
                    Input = "Summarize the runtime configuration",
                    WorkingDirectory = workspaceRoot,
                    RuntimeProfile = runtimeProfile
                });

            Assert.NotNull(result);
            Assert.Equal("text-embedding-v4", result!.Model);
            Assert.Equal([0.1f, 0.2f, 0.3f], result.Embedding);
            Assert.NotNull(capturedRequest);
            Assert.Equal("https://dashscope.aliyuncs.com/compatible-mode/v1/embeddings", capturedRequest!.RequestUri!.ToString());

            var payload = JsonNode.Parse(capturedPayload!)!.AsObject();
            Assert.Equal("text-embedding-v4", payload["model"]?.GetValue<string>());
            Assert.Equal("Summarize the runtime configuration", payload["input"]?.GetValue<string>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
