using QwenCode.Core.Config;

namespace QwenCode.Tests.Tools;

public sealed class AgentToolTests
{
    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Agent_PersistsExecutionRecordAndReturnsHeadlessRuntimeReport()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-agent-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);
            await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "App.cs"), "namespace Demo; public sealed class App {}");

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var host = CreateToolExecutor(workspaceRoot, homeRoot, systemRoot);

            var taskCreateResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "task_create",
                ApproveExecution = true,
                ArgumentsJson =
                    """
                    {
                      "subject":"Explore runtime",
                      "description":"Inspect the runtime module and prepare a concise execution brief",
                      "owner":"planner"
                    }
                    """
            });
            Assert.Equal("completed", taskCreateResult.Status);

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "agent",
                ApproveExecution = true,
                ArgumentsJson =
                    """
                    {
                      "description":"Explore runtime",
                      "prompt":"Inspect the runtime module and prepare a concise execution brief",
                      "subagent_type":"Explore",
                      "task_id":"1"
                    }
                    """
            });

            Assert.Equal("completed", result.Status);
            Assert.Equal("agent", result.ToolName);
            Assert.Contains("Subagent 'Explore' finished with status 'completed'.", result.Output);
            Assert.Contains("Assistant summary:", result.Output);
            Assert.Equal(2, result.ChangedFiles.Count);

            var artifactPath = result.ChangedFiles.Single(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            var transcriptPath = result.ChangedFiles.Single(path => path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(artifactPath));
            Assert.True(File.Exists(transcriptPath));

            var persisted = await File.ReadAllTextAsync(artifactPath);
            Assert.Contains("\"AgentName\": \"Explore\"", persisted);
            Assert.Contains("Inspect the runtime module", persisted);
            Assert.Contains("\"ProviderName\": \"fallback\"", persisted);
            Assert.Contains("\"StopReason\": \"completed\"", persisted);
            Assert.Contains("\"RoundCount\": 1", persisted);
            Assert.Contains("\"TranscriptPath\":", persisted);
            Assert.Contains("\"TaskId\": \"1\"", persisted);

            var taskFilePath = taskCreateResult.ChangedFiles.Single();
            var taskFile = await File.ReadAllTextAsync(taskFilePath);
            Assert.Contains("\"Id\": \"1\"", taskFile);
            Assert.Contains("\"Status\": \"completed\"", taskFile);
            Assert.Contains("\"Owner\": \"Explore\"", taskFile);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Agent_ReturnsErrorForUnknownSubagent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-agent-tool-error-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var host = CreateToolExecutor(workspaceRoot, homeRoot, systemRoot);

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "agent",
                ApproveExecution = true,
                ArgumentsJson =
                    """
                    {
                      "description":"Unknown delegate",
                      "prompt":"Try to use a missing subagent",
                      "subagent_type":"missing-agent"
                    }
                    """
            });

            Assert.Equal("error", result.Status);
            Assert.Contains("Available subagents", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Agent_EmitsNestedSubagentRuntimeEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-agent-tool-events-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var host = CreateToolExecutor(workspaceRoot, homeRoot, systemRoot);
            var events = new List<AssistantRuntimeEvent>();

            var result = await host.ExecuteAsync(
                sourcePaths,
                new ExecuteNativeToolRequest
                {
                    ToolName = "agent",
                    ApproveExecution = true,
                    ArgumentsJson =
                        """
                        {
                          "description":"Explore runtime",
                          "prompt":"Inspect the runtime module and prepare a concise execution brief",
                          "subagent_type":"Explore"
                        }
                        """
                },
                events.Add);

            Assert.Equal("completed", result.Status);
            Assert.NotEmpty(events);
            Assert.Contains(events, item => item.AgentName == "Explore" && item.Message.Contains("is starting", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(events, item => item.AgentName == "Explore" && item.Stage == "assembling-context");
            Assert.Contains(events, item => item.AgentName == "Explore" && item.Message.Contains("finished with status 'completed'", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IToolExecutor CreateToolExecutor(string workspaceRoot, string homeRoot, string systemRoot)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDesktopEnvironmentPaths>(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot, AppContext.BaseDirectory));
        services.AddOptions<NativeAssistantRuntimeOptions>()
            .Configure(options => options.Provider = "fallback");
        services.AddInfrastructureServices();
        services.AddConfigServices();
        services.AddCompatibilityServices();
        services.AddPermissionServices();
        services.AddRuntimeServices();
        services.AddAgentServices();
        services.AddToolServices();

        return services.BuildServiceProvider().GetRequiredService<IToolExecutor>();
    }
}
