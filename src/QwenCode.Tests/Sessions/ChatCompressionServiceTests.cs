namespace QwenCode.Tests.Sessions;

public sealed class ChatCompressionServiceTests
{
    [Fact]
    public async Task TryCreateCheckpointAsync_UsesRuntimeProfileThresholdAndProducesTokenMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-chat-compression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var transcriptPath = Path.Combine(root, "compression.jsonl");
            await using (var writer = new StreamWriter(transcriptPath))
            {
                for (var index = 0; index < 16; index++)
                {
                    await writer.WriteLineAsync(
                        JsonSerializer.Serialize(new
                        {
                            uuid = Guid.NewGuid().ToString(),
                            sessionId = "compression-session",
                            timestamp = DateTime.UtcNow.AddMinutes(-16 + index).ToString("O"),
                            type = index % 2 == 0 ? "user" : "assistant",
                            status = "completed",
                            toolName = string.Empty,
                            message = new
                            {
                                parts = new[]
                                {
                                    new
                                    {
                                        text = new string('x', 1200)
                                    }
                                }
                            }
                        }));
                }
            }

            var service = new ChatCompressionService();
            var checkpoint = await service.TryCreateCheckpointAsync(
                new QwenRuntimeProfile
                {
                    ProjectRoot = root,
                    GlobalQwenDirectory = root,
                    RuntimeBaseDirectory = root,
                    RuntimeSource = "test",
                    ProjectDataDirectory = root,
                    ChatsDirectory = root,
                    HistoryDirectory = root,
                    ContextFileNames = ["QWEN.md"],
                    ContextFilePaths = [Path.Combine(root, "QWEN.md")],
                    ModelName = "qwen3-coder-plus",
                    ChatCompression = new RuntimeChatCompressionSettings
                    {
                        ContextPercentageThreshold = 0.03
                    },
                    Checkpointing = true,
                    ApprovalProfile = new ApprovalProfile
                    {
                        DefaultMode = "default",
                        ConfirmShellCommands = true,
                        ConfirmFileEdits = true,
                        AllowRules = [],
                        AskRules = [],
                        DenyRules = []
                    }
                },
                transcriptPath);

            Assert.NotNull(checkpoint);
            Assert.Equal("context-threshold", checkpoint!.Trigger);
            Assert.True(checkpoint.EstimatedTokenCount > 0);
            Assert.True(checkpoint.EstimatedContextWindowTokens > 0);
            Assert.True(checkpoint.EstimatedContextPercentage >= 0.03d);
            Assert.Equal(0.03d, checkpoint.ThresholdPercentage);
            Assert.Contains("tokens", checkpoint.Summary);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryCreateCheckpointAsync_SkipsWhenCheckpointingIsDisabled()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-chat-compression-disabled-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var transcriptPath = Path.Combine(root, "compression.jsonl");
            await File.WriteAllTextAsync(
                transcriptPath,
                JsonSerializer.Serialize(new
                {
                    uuid = Guid.NewGuid().ToString(),
                    sessionId = "compression-session",
                    timestamp = DateTime.UtcNow.ToString("O"),
                    type = "user",
                    status = "completed",
                    message = new
                    {
                        parts = new[]
                        {
                            new { text = new string('x', 3000) }
                        }
                    }
                }));

            var service = new ChatCompressionService();
            var checkpoint = await service.TryCreateCheckpointAsync(
                new QwenRuntimeProfile
                {
                    ProjectRoot = root,
                    GlobalQwenDirectory = root,
                    RuntimeBaseDirectory = root,
                    RuntimeSource = "test",
                    ProjectDataDirectory = root,
                    ChatsDirectory = root,
                    HistoryDirectory = root,
                    ContextFileNames = ["QWEN.md"],
                    ContextFilePaths = [Path.Combine(root, "QWEN.md")],
                    Checkpointing = false,
                    ApprovalProfile = new ApprovalProfile
                    {
                        DefaultMode = "default",
                        ConfirmShellCommands = true,
                        ConfirmFileEdits = true,
                        AllowRules = [],
                        AskRules = [],
                        DenyRules = []
                    }
                },
                transcriptPath);

            Assert.Null(checkpoint);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryCreateCheckpointAsync_UsesLlmSummaryWhenAvailable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-chat-compression-llm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var transcriptPath = Path.Combine(root, "compression.jsonl");
            await using (var writer = new StreamWriter(transcriptPath))
            {
                for (var index = 0; index < 16; index++)
                {
                    await writer.WriteLineAsync(
                        JsonSerializer.Serialize(new
                        {
                            uuid = Guid.NewGuid().ToString(),
                            sessionId = "compression-session",
                            timestamp = DateTime.UtcNow.AddMinutes(-16 + index).ToString("O"),
                            type = index % 2 == 0 ? "user" : "assistant",
                            status = "completed",
                            toolName = string.Empty,
                            message = new
                            {
                                parts = new[]
                                {
                                    new
                                    {
                                        text = $"Important historical step {index} involving src/App{index}.cs, dotnet test, runtime diagnostics, transcript hydration, tool recovery, and checkpoint maintenance across the desktop chat host."
                                    }
                                }
                            }
                        }));
                }
            }

            var service = new ChatCompressionService(new StaticCompressionContentGenerator());
            var checkpoint = await service.TryCreateCheckpointAsync(
                new QwenRuntimeProfile
                {
                    ProjectRoot = root,
                    GlobalQwenDirectory = root,
                    RuntimeBaseDirectory = root,
                    RuntimeSource = "test",
                    ProjectDataDirectory = root,
                    ChatsDirectory = root,
                    HistoryDirectory = root,
                    ContextFileNames = ["QWEN.md"],
                    ContextFilePaths = [Path.Combine(root, "QWEN.md")],
                    ModelName = "qwen3-coder-plus",
                    CurrentLocale = "en-US",
                    CurrentLanguage = "English",
                    ChatCompression = new RuntimeChatCompressionSettings
                    {
                        ContextPercentageThreshold = 0.001
                    },
                    Checkpointing = true,
                    ApprovalProfile = new ApprovalProfile
                    {
                        DefaultMode = "default",
                        ConfirmShellCommands = true,
                        ConfirmFileEdits = true,
                        AllowRules = [],
                        AskRules = [],
                        DenyRules = []
                    }
                },
                transcriptPath);

            Assert.NotNull(checkpoint);
            Assert.Contains("Compression checkpoint:", checkpoint!.Summary, StringComparison.Ordinal);
            Assert.Contains("- Goal: stabilize desktop chat runtime", checkpoint.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain("Important historical step 0", checkpoint.Summary, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StaticCompressionContentGenerator : IContentGenerator
    {
        public Task<LlmContentResponse?> GenerateContentAsync(
            LlmContentRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LlmContentResponse?>(new LlmContentResponse
            {
                Content =
                """
                - Goal: stabilize desktop chat runtime
                - Decision: prefer native tools over shell when possible
                - Validation: rerun dotnet test after major edits
                """,
                ProviderName = "test",
                Model = "test-model"
            });

        public Task<JsonObject?> GenerateJsonAsync(JsonGenerationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<JsonObject?>(null);

        public Task<EmbeddingGenerationResponse?> GenerateEmbeddingAsync(EmbeddingGenerationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<EmbeddingGenerationResponse?>(null);
    }
}
