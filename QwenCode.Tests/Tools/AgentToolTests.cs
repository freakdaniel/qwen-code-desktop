namespace QwenCode.Tests.Tools;

public sealed class AgentToolTests
{
    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_Agent_PersistsExecutionRecordAndReturnsReport()
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
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var approvalPolicy = new ApprovalPolicyService();
            var host = new NativeToolHostService(runtimeProfileService, approvalPolicy);

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
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
            });

            Assert.Equal("completed", result.Status);
            Assert.Equal("agent", result.ToolName);
            Assert.Contains("Subagent 'Explore' completed", result.Output);
            var changedFile = Assert.Single(result.ChangedFiles);
            Assert.True(File.Exists(changedFile));
            var persisted = await File.ReadAllTextAsync(changedFile);
            Assert.Contains("\"AgentName\": \"Explore\"", persisted);
            Assert.Contains("Inspect the runtime module", persisted);
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
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService());

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
}
