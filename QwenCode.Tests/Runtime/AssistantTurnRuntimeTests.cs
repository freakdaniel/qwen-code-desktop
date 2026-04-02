namespace QwenCode.Tests.Runtime;

public sealed class AssistantTurnRuntimeTests
{
    [Fact]
    public async Task AssistantTurnRuntime_ExecutesToolCallsAndReturnsFinalSummary()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-assistant-tool-loop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var targetFile = Path.Combine(workspaceRoot, "sample.txt");
            File.WriteAllText(targetFile, "native tool content");

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var approvalPolicyService = new ApprovalPolicyService();
            var runtime = CreateAssistantTurnRuntime(
                new ToolCallingAssistantResponseProvider(targetFile),
                new NativeToolHostService(runtimeProfileService, approvalPolicyService));

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var response = await runtime.GenerateAsync(
                new AssistantTurnRequest
                {
                    SessionId = "tool-loop-session",
                    Prompt = "Inspect the target file and summarize the result.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "tool-loop-session.jsonl"),
                    RuntimeProfile = runtimeProfile,
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                });

            Assert.Equal("tool-provider", response.ProviderName);
            Assert.Equal("Tool loop complete after 1 native execution(s).", response.Summary);
            var toolExecution = Assert.Single(response.ToolExecutions);
            Assert.Equal("read_file", toolExecution.Execution.ToolName);
            Assert.Equal("completed", toolExecution.Execution.Status);
            Assert.Contains("native tool content", toolExecution.Execution.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
