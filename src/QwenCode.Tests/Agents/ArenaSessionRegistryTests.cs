namespace QwenCode.Tests.Agents;

public sealed class ArenaSessionRegistryTests
{
    [Fact]
    public void ArenaSessionRegistry_UpdateAndComplete_PublishExpectedSnapshots()
    {
        var registry = new ArenaSessionRegistry();
        var events = new List<ArenaSessionEvent>();
        registry.SessionEvent += (_, sessionEvent) => events.Add(sessionEvent);

        registry.Start(
            new ActiveArenaSessionState
            {
                SessionId = "arena-session",
                Task = "Test task",
                Status = "running",
                WorkingDirectory = @"E:\workspace",
                BaseBranch = "main",
                RoundCount = 1,
                StartedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow,
                Stats = new ArenaSessionStats
                {
                    AgentCount = 1,
                    RoundCount = 1
                },
                Agents =
                [
                    new ArenaAgentStatusFile
                    {
                        AgentId = "arena-session/alpha",
                        AgentName = "alpha",
                        Status = "initializing",
                        Model = "model-a",
                        WorktreeName = "alpha",
                        WorktreePath = @"E:\workspace-alpha",
                        Branch = "arena/alpha",
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                ]
            },
            new CancellationTokenSource(),
            "Started.");

        registry.Update(
            "arena-session",
            state =>
            {
                state.RoundCount = 2;
                state.SelectedWinner = "alpha";
                state.Stats = new ArenaSessionStats
                {
                    AgentCount = 1,
                    CompletedAgentCount = 1,
                    RoundCount = 2,
                    ToolCallCount = 3,
                    SuccessfulToolCallCount = 2,
                    FailedToolCallCount = 1,
                    TotalDurationMs = 500
                };
                state.Agents =
                [
                    new ArenaAgentStatusFile
                    {
                        AgentId = "arena-session/alpha",
                        AgentName = "alpha",
                        Status = "completed",
                        Model = "model-a",
                        WorktreeName = "alpha",
                        WorktreePath = @"E:\workspace-alpha",
                        Branch = "arena/alpha",
                        ProviderName = "fake",
                        FinalSummary = "Done",
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                ];
            },
            ArenaSessionEventKind.RoundCompleted,
            "Round completed.");

        var activeSession = registry.ListActiveSessions().Single();
        Assert.Equal(2, activeSession.RoundCount);
        Assert.Equal("alpha", activeSession.SelectedWinner);
        Assert.Equal("completed", activeSession.Agents.Single().Status);
        Assert.Equal(3, activeSession.Stats.ToolCallCount);

        registry.Complete(
            "arena-session",
            "completed",
            2,
            "alpha",
            activeSession.Stats,
            activeSession.Agents,
            "Finished.");

        Assert.Empty(registry.ListActiveSessions());
        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal(ArenaSessionEventKind.SessionStarted, first.Kind);
                Assert.Equal("running", first.Status);
            },
            second =>
            {
                Assert.Equal(ArenaSessionEventKind.RoundCompleted, second.Kind);
                Assert.Equal(2, second.RoundCount);
                Assert.Equal("alpha", second.SelectedWinner);
                Assert.Equal(3, second.Stats.ToolCallCount);
            },
            third =>
            {
                Assert.Equal(ArenaSessionEventKind.SessionCompleted, third.Kind);
                Assert.Equal("completed", third.Status);
                Assert.Equal(2, third.RoundCount);
                Assert.Equal("alpha", third.SelectedWinner);
                Assert.Equal(3, third.Stats.ToolCallCount);
            });
    }

    [Fact]
    public void ArenaSessionRegistry_Cancel_PublishesCancellingState()
    {
        var registry = new ArenaSessionRegistry();
        var events = new List<ArenaSessionEvent>();
        registry.SessionEvent += (_, sessionEvent) => events.Add(sessionEvent);

        registry.Start(
            new ActiveArenaSessionState
            {
                SessionId = "arena-cancel",
                Task = "Cancel task",
                Status = "running",
                WorkingDirectory = @"E:\workspace",
                BaseBranch = "main",
                StartedAtUtc = DateTime.UtcNow,
                LastUpdatedAtUtc = DateTime.UtcNow,
                Stats = new ArenaSessionStats
                {
                    AgentCount = 0,
                    RoundCount = 0
                },
                Agents = []
            },
            new CancellationTokenSource(),
            "Started.");

        var cancelled = registry.Cancel("arena-cancel", "Cancellation requested.");

        Assert.True(cancelled);
        var active = registry.ListActiveSessions().Single();
        Assert.Equal("cancelling", active.Status);
        Assert.Collection(
            events,
            first => Assert.Equal(ArenaSessionEventKind.SessionStarted, first.Kind),
            second =>
            {
                Assert.Equal(ArenaSessionEventKind.SessionUpdated, second.Kind);
                Assert.Equal("cancelling", second.Status);
            });
    }
}
