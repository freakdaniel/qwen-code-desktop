namespace QwenCode.Tests.Sessions;

public sealed class ActiveTurnRegistryTests
{
    [Fact]
    public async Task RunAsync_DuringActiveOperation_ExposesUpdatableSnapshot()
    {
        var registry = new ActiveTurnRegistry();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var runTask = registry.RunAsync(
            "session-1",
            new ActiveTurnState
            {
                SessionId = "session-1",
                Prompt = "Investigate reconnect state.",
                TranscriptPath = Path.Combine(Path.GetTempPath(), "session-1.jsonl"),
                WorkingDirectory = "E:\\workspace",
                GitBranch = "main",
                ToolName = string.Empty,
                Stage = "turn-started",
                Status = "started",
                ContentSnapshot = string.Empty,
                StartedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow
            },
            async cancellationToken =>
            {
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
                return 42;
            },
            () => Task.FromResult(-1));

        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        registry.Update("session-1", state =>
        {
            state.Stage = "response-delta";
            state.Status = "streaming";
            state.ContentSnapshot = "Partial assistant response.";
        });

        var activeTurns = registry.ListActiveTurns();
        var activeTurn = Assert.Single(activeTurns);
        Assert.Equal("session-1", activeTurn.SessionId);
        Assert.Equal("response-delta", activeTurn.Stage);
        Assert.Equal("streaming", activeTurn.Status);
        Assert.Equal("Partial assistant response.", activeTurn.ContentSnapshot);

        release.TrySetResult();

        var result = await runTask;
        Assert.Equal(42, result);
        Assert.Empty(registry.ListActiveTurns());
    }
}
