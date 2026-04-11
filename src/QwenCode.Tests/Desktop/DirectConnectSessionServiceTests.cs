using QwenCode.App.Desktop;
using QwenCode.App.Desktop.DirectConnect;
using QwenCode.Core.Models;

namespace QwenCode.Tests.Desktop;

public sealed class DirectConnectSessionServiceTests
{
    [Fact]
    public async Task StartTurnAsync_BindsSessionAndBuffersMatchingEvents()
    {
        var projection = new RecordingSessionProjection();
        var service = new DirectConnectSessionService(projection);

        var directSession = await service.CreateSessionAsync(new CreateDirectConnectSessionRequest
        {
            WorkingDirectory = "D:\\Projects\\workspace"
        });

        var result = await service.StartTurnAsync(
            directSession.DirectConnectSessionId,
            new StartDesktopSessionTurnRequest
            {
                Prompt = "Inspect the repository",
                WorkingDirectory = string.Empty
            });

        Assert.False(string.IsNullOrWhiteSpace(projection.LastStartRequest?.SessionId));
        Assert.Equal("D:\\Projects\\workspace", projection.LastStartRequest?.WorkingDirectory);
        Assert.Equal(projection.LastStartRequest?.SessionId, result.Session.SessionId);

        var snapshot = await service.GetSessionAsync(directSession.DirectConnectSessionId);
        Assert.NotNull(snapshot);
        Assert.Equal(result.Session.SessionId, snapshot!.BoundSessionId);
        Assert.Equal(1, snapshot.LatestEventSequence);

        var firstBatch = await service.ReadEventsAsync(directSession.DirectConnectSessionId);
        var firstEvent = Assert.Single(firstBatch.Events);
        Assert.Equal(1, firstEvent.Sequence);
        Assert.Equal(result.Session.SessionId, firstEvent.Event.SessionId);

        var secondBatch = await service.ReadEventsAsync(
            directSession.DirectConnectSessionId,
            afterSequence: firstBatch.LatestSequence);
        Assert.Empty(secondBatch.Events);
    }

    [Fact]
    public async Task ApprovePendingToolAsync_UsesBoundSessionIdWhenRequestOmitsOne()
    {
        var projection = new RecordingSessionProjection();
        var service = new DirectConnectSessionService(projection);

        var directSession = await service.CreateSessionAsync(new CreateDirectConnectSessionRequest
        {
            PreferredSessionId = "desktop-session-1"
        });

        await service.ApprovePendingToolAsync(
            directSession.DirectConnectSessionId,
            new ApproveDesktopSessionToolRequest
            {
                EntryId = "tool-1",
                Decision = "allow-once"
            });

        Assert.NotNull(projection.LastApproveRequest);
        Assert.Equal("desktop-session-1", projection.LastApproveRequest!.SessionId);
        Assert.Equal("tool-1", projection.LastApproveRequest.EntryId);
    }

    [Fact]
    public async Task StreamEventsAsync_YieldsLiveEventsFromBoundSession()
    {
        var projection = new RecordingSessionProjection();
        var service = new DirectConnectSessionService(projection);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var directSession = await service.CreateSessionAsync(new CreateDirectConnectSessionRequest
        {
            WorkingDirectory = "D:\\Projects\\workspace"
        }, timeout.Token);

        await using var stream = service
            .StreamEventsAsync(directSession.DirectConnectSessionId, cancellationToken: timeout.Token)
            .GetAsyncEnumerator(timeout.Token);
        var nextEvent = stream.MoveNextAsync().AsTask();

        await service.StartTurnAsync(
            directSession.DirectConnectSessionId,
            new StartDesktopSessionTurnRequest
            {
                Prompt = "Stream the first event"
            },
            timeout.Token);

        Assert.True(await nextEvent.WaitAsync(timeout.Token));
        Assert.Equal(1, stream.Current.Sequence);
        Assert.Equal(DesktopSessionEventKind.TurnStarted, stream.Current.Event.Kind);
    }

    private sealed class RecordingSessionProjection : IDesktopSessionProjectionService
    {
        public event EventHandler<DesktopSessionEvent>? SessionEvent;

        public StartDesktopSessionTurnRequest? LastStartRequest { get; private set; }
        public ApproveDesktopSessionToolRequest? LastApproveRequest { get; private set; }

        public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() =>
            Task.FromResult<IReadOnlyList<ActiveTurnState>>([]);

        public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
            Task.FromResult<DesktopSessionDetail?>(null);

        public Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request) =>
            throw new NotSupportedException();

        public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request)
        {
            LastStartRequest = request;
            SessionEvent?.Invoke(this, new DesktopSessionEvent
            {
                SessionId = request.SessionId,
                Kind = DesktopSessionEventKind.TurnStarted,
                TimestampUtc = DateTime.UtcNow,
                Message = "Turn started",
                WorkingDirectory = request.WorkingDirectory
            });

            return Task.FromResult(new DesktopSessionTurnResult
            {
                CreatedNewSession = true,
                AssistantSummary = string.Empty,
                Session = new SessionPreview
                {
                    SessionId = request.SessionId,
                    Title = null,
                    LastActivity = DateTime.UtcNow.ToString("O"),
                    StartedAt = DateTime.UtcNow.ToString("O"),
                    LastUpdatedAt = DateTime.UtcNow.ToString("O"),
                    Category = "session",
                    Mode = DesktopMode.Code,
                    Status = "active",
                    WorkingDirectory = request.WorkingDirectory,
                    GitBranch = "main",
                    MessageCount = 1,
                    TranscriptPath = "chat.jsonl",
                    MetadataPath = "chat.meta.json"
                },
                ToolExecution = new NativeToolExecutionResult
                {
                    ToolName = "chat",
                    Status = "completed",
                    ApprovalState = "approved",
                    WorkingDirectory = request.WorkingDirectory,
                    ChangedFiles = []
                }
            });
        }

        public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request)
        {
            LastApproveRequest = request;
            return Task.FromResult(new DesktopSessionTurnResult
            {
                CreatedNewSession = false,
                AssistantSummary = string.Empty,
                Session = new SessionPreview
                {
                    SessionId = request.SessionId,
                    Title = null,
                    LastActivity = DateTime.UtcNow.ToString("O"),
                    StartedAt = DateTime.UtcNow.ToString("O"),
                    LastUpdatedAt = DateTime.UtcNow.ToString("O"),
                    Category = "session",
                    Mode = DesktopMode.Code,
                    Status = "active",
                    WorkingDirectory = "D:\\Projects\\workspace",
                    GitBranch = "main",
                    MessageCount = 1,
                    TranscriptPath = "chat.jsonl",
                    MetadataPath = "chat.meta.json"
                },
                ToolExecution = new NativeToolExecutionResult
                {
                    ToolName = "run_shell_command",
                    Status = "completed",
                    ApprovalState = "approved",
                    WorkingDirectory = "D:\\Projects\\workspace",
                    ChangedFiles = []
                }
            });
        }

        public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) =>
            throw new NotSupportedException();

        public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
            throw new NotSupportedException();

        public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
            throw new NotSupportedException();
    }
}
