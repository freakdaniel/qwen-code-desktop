namespace QwenCode.Tests.Shared.Fakes;

internal sealed class FakeToolExecutor : IToolExecutor
{
    public NativeToolHostSnapshot Inspect(WorkspacePaths paths) => new()
    {
        RegisteredCount = 0,
        ImplementedCount = 0,
        ReadyCount = 0,
        ApprovalRequiredCount = 0,
        Tools = []
    };

    public Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        ExecuteNativeToolRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new NativeToolExecutionResult
        {
            ToolName = request.ToolName,
            Status = "error",
            ApprovalState = "deny",
            WorkingDirectory = paths.WorkspaceRoot,
            Output = string.Empty,
            ErrorMessage = "Fake tool executor should not be used for this test path.",
            ExitCode = 1,
            ChangedFiles = []
        });
}
