using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using QwenCode.Core.Models;

namespace QwenCode.App.Desktop.DirectConnect;

/// <summary>
/// Provides transport-agnostic orchestration for direct-connect sessions.
/// </summary>
public sealed class DirectConnectSessionService : IDirectConnectSessionService, IDisposable
{
    private const int DefaultMaxEvents = 100;
    private const int MaxBufferedEventsPerSession = 512;

    private readonly IDesktopSessionProjectionService projectionService;
    private readonly ConcurrentDictionary<string, ManagedDirectConnectSession> sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of <see cref="DirectConnectSessionService"/>.
    /// </summary>
    /// <param name="projectionService">The desktop session projection service.</param>
    public DirectConnectSessionService(IDesktopSessionProjectionService projectionService)
    {
        this.projectionService = projectionService;
        this.projectionService.SessionEvent += OnSessionEvent;
    }

    /// <inheritdoc />
    public Task<DirectConnectSessionState> CreateSessionAsync(
        CreateDirectConnectSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.UtcNow;
        var directConnectSessionId = Guid.NewGuid().ToString("N");
        var snapshot = new DirectConnectSessionState
        {
            DirectConnectSessionId = directConnectSessionId,
            BoundSessionId = request.PreferredSessionId?.Trim() ?? string.Empty,
            WorkingDirectory = request.WorkingDirectory?.Trim() ?? string.Empty,
            Status = "active",
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            LatestEventSequence = 0
        };

        sessions[directConnectSessionId] = new ManagedDirectConnectSession(snapshot);
        return Task.FromResult(snapshot);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DirectConnectSessionState>> ListSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshots = sessions.Values
            .Select(static session => session.GetSnapshot())
            .OrderByDescending(static session => session.LastActivityAtUtc)
            .ToArray();
        return Task.FromResult<IReadOnlyList<DirectConnectSessionState>>(snapshots);
    }

    /// <inheritdoc />
    public Task<DirectConnectSessionState?> GetSessionAsync(
        string directConnectSessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            sessions.TryGetValue(directConnectSessionId, out var session)
                ? session.GetSnapshot()
                : null);
    }

    /// <inheritdoc />
    public Task<DirectConnectSessionEventBatch> ReadEventsAsync(
        string directConnectSessionId,
        long afterSequence = 0,
        int maxCount = DefaultMaxEvents,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetRequiredSession(directConnectSessionId).ReadEvents(afterSequence, maxCount));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DirectConnectSessionEventRecord> StreamEventsAsync(
        string directConnectSessionId,
        long afterSequence = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = GetRequiredSession(directConnectSessionId);
        var cursor = Math.Max(0, afterSequence);

        while (session.IsActive)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = session.ReadEvents(cursor, DefaultMaxEvents);
            foreach (var record in batch.Events)
            {
                cursor = record.Sequence;
                yield return record;
            }

            if (!session.IsActive)
            {
                yield break;
            }

            await session.WaitForNextEventAsync(cursor, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<DesktopSessionTurnResult> StartTurnAsync(
        string directConnectSessionId,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = GetRequiredSession(directConnectSessionId);
        var effectiveSessionId = session.ResolveOrAllocateSessionId(request.SessionId);
        var effectiveWorkingDirectory = !string.IsNullOrWhiteSpace(request.WorkingDirectory)
            ? request.WorkingDirectory
            : session.GetSnapshot().WorkingDirectory;

        session.BindSession(effectiveSessionId, effectiveWorkingDirectory);

        var result = await projectionService.StartSessionTurnAsync(new StartDesktopSessionTurnRequest
        {
            SessionId = effectiveSessionId,
            Prompt = request.Prompt,
            WorkingDirectory = effectiveWorkingDirectory,
            SurfaceContext = request.SurfaceContext,
            ToolName = request.ToolName,
            ToolArgumentsJson = request.ToolArgumentsJson,
            ApproveToolExecution = request.ApproveToolExecution
        });

        session.BindSession(
            string.IsNullOrWhiteSpace(result.Session?.SessionId) ? effectiveSessionId : result.Session.SessionId,
            string.IsNullOrWhiteSpace(result.Session?.WorkingDirectory) ? effectiveWorkingDirectory : result.Session.WorkingDirectory);
        return result;
    }

    /// <inheritdoc />
    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        string directConnectSessionId,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveSessionId = GetRequiredSession(directConnectSessionId).ResolveExistingSessionId(request.SessionId);
        return projectionService.ApprovePendingToolAsync(new ApproveDesktopSessionToolRequest
        {
            SessionId = effectiveSessionId,
            EntryId = request.EntryId,
            Decision = request.Decision,
            Feedback = request.Feedback
        });
    }

    /// <inheritdoc />
    public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
        string directConnectSessionId,
        AnswerDesktopSessionQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveSessionId = GetRequiredSession(directConnectSessionId).ResolveExistingSessionId(request.SessionId);
        return projectionService.AnswerPendingQuestionAsync(new AnswerDesktopSessionQuestionRequest
        {
            SessionId = effectiveSessionId,
            EntryId = request.EntryId,
            Answers = request.Answers
        });
    }

    /// <inheritdoc />
    public Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
        string directConnectSessionId,
        CancelDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveSessionId = GetRequiredSession(directConnectSessionId).ResolveExistingSessionId(request.SessionId);
        return projectionService.CancelSessionTurnAsync(new CancelDesktopSessionTurnRequest
        {
            SessionId = effectiveSessionId
        });
    }

    /// <inheritdoc />
    public async Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
        string directConnectSessionId,
        ResumeInterruptedTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = GetRequiredSession(directConnectSessionId);
        var effectiveSessionId = session.ResolveExistingSessionId(request.SessionId);
        var result = await projectionService.ResumeInterruptedTurnAsync(new ResumeInterruptedTurnRequest
        {
            SessionId = effectiveSessionId,
            RecoveryNote = request.RecoveryNote
        });

        session.BindSession(effectiveSessionId, result.Session?.WorkingDirectory ?? session.GetSnapshot().WorkingDirectory);
        return result;
    }

    /// <inheritdoc />
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
        string directConnectSessionId,
        DismissInterruptedTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveSessionId = GetRequiredSession(directConnectSessionId).ResolveExistingSessionId(request.SessionId);
        return projectionService.DismissInterruptedTurnAsync(new DismissInterruptedTurnRequest
        {
            SessionId = effectiveSessionId
        });
    }

    /// <inheritdoc />
    public Task<DirectConnectSessionState> CloseSessionAsync(
        string directConnectSessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!sessions.TryRemove(directConnectSessionId, out var session))
        {
            throw new InvalidOperationException($"Direct-connect session '{directConnectSessionId}' was not found.");
        }

        return Task.FromResult(session.Close());
    }

    /// <inheritdoc />
    public void Dispose() => projectionService.SessionEvent -= OnSessionEvent;

    private ManagedDirectConnectSession GetRequiredSession(string directConnectSessionId)
    {
        if (!sessions.TryGetValue(directConnectSessionId, out var session))
        {
            throw new InvalidOperationException($"Direct-connect session '{directConnectSessionId}' was not found.");
        }

        return session;
    }

    private void OnSessionEvent(object? sender, DesktopSessionEvent sessionEvent)
    {
        foreach (var session in sessions.Values)
        {
            session.TryAppendEvent(sessionEvent);
        }
    }

    private sealed class ManagedDirectConnectSession
    {
        private readonly object gate = new();
        private readonly List<DirectConnectSessionEventRecord> events = [];
        private TaskCompletionSource<object?> eventSignal = CreateEventSignal();

        private readonly string directConnectSessionId;
        private readonly DateTime createdAtUtc;
        private string boundSessionId;
        private string workingDirectory;
        private string status;
        private DateTime lastActivityAtUtc;
        private long latestEventSequence;

        public ManagedDirectConnectSession(DirectConnectSessionState snapshot)
        {
            directConnectSessionId = snapshot.DirectConnectSessionId;
            createdAtUtc = snapshot.CreatedAtUtc;
            boundSessionId = snapshot.BoundSessionId;
            workingDirectory = snapshot.WorkingDirectory;
            status = snapshot.Status;
            lastActivityAtUtc = snapshot.LastActivityAtUtc;
            latestEventSequence = snapshot.LatestEventSequence;
        }

        public bool IsActive
        {
            get
            {
                lock (gate)
                {
                    return status == "active";
                }
            }
        }

        public DirectConnectSessionState GetSnapshot()
        {
            lock (gate)
            {
                return new DirectConnectSessionState
                {
                    DirectConnectSessionId = directConnectSessionId,
                    BoundSessionId = boundSessionId,
                    WorkingDirectory = workingDirectory,
                    Status = status,
                    CreatedAtUtc = createdAtUtc,
                    LastActivityAtUtc = lastActivityAtUtc,
                    LatestEventSequence = latestEventSequence
                };
            }
        }

        public string ResolveOrAllocateSessionId(string requestedSessionId)
        {
            lock (gate)
            {
                if (!string.IsNullOrWhiteSpace(requestedSessionId))
                {
                    return requestedSessionId.Trim();
                }

                if (!string.IsNullOrWhiteSpace(boundSessionId))
                {
                    return boundSessionId;
                }

                return Guid.NewGuid().ToString();
            }
        }

        public string ResolveExistingSessionId(string requestedSessionId)
        {
            lock (gate)
            {
                var resolved = !string.IsNullOrWhiteSpace(requestedSessionId)
                    ? requestedSessionId.Trim()
                    : boundSessionId;

                if (string.IsNullOrWhiteSpace(resolved))
                {
                    throw new InvalidOperationException("This direct-connect session is not yet bound to a desktop session.");
                }

                lastActivityAtUtc = DateTime.UtcNow;
                return resolved;
            }
        }

        public void BindSession(string sessionId, string preferredWorkingDirectory)
        {
            lock (gate)
            {
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    boundSessionId = sessionId.Trim();
                }

                if (!string.IsNullOrWhiteSpace(preferredWorkingDirectory))
                {
                    workingDirectory = preferredWorkingDirectory.Trim();
                }

                status = "active";
                lastActivityAtUtc = DateTime.UtcNow;
            }
        }

        public void TryAppendEvent(DesktopSessionEvent sessionEvent)
        {
            lock (gate)
            {
                if (status != "active" ||
                    string.IsNullOrWhiteSpace(boundSessionId) ||
                    !string.Equals(boundSessionId, sessionEvent.SessionId, StringComparison.Ordinal))
                {
                    return;
                }

                latestEventSequence++;
                events.Add(new DirectConnectSessionEventRecord
                {
                    Sequence = latestEventSequence,
                    Event = sessionEvent
                });
                eventSignal.TrySetResult(null);
                eventSignal = CreateEventSignal();

                if (events.Count > MaxBufferedEventsPerSession)
                {
                    events.RemoveRange(0, events.Count - MaxBufferedEventsPerSession);
                }

                lastActivityAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(sessionEvent.WorkingDirectory))
                {
                    workingDirectory = sessionEvent.WorkingDirectory;
                }
            }
        }

        public DirectConnectSessionEventBatch ReadEvents(long afterSequence, int maxCount)
        {
            lock (gate)
            {
                var effectiveMax = maxCount <= 0 ? DefaultMaxEvents : Math.Min(maxCount, MaxBufferedEventsPerSession);
                var batch = events
                    .Where(record => record.Sequence > afterSequence)
                    .Take(effectiveMax)
                    .ToArray();

                lastActivityAtUtc = DateTime.UtcNow;
                return new DirectConnectSessionEventBatch
                {
                    DirectConnectSessionId = directConnectSessionId,
                    LatestSequence = latestEventSequence,
                    Events = batch
                };
            }
        }

        public Task WaitForNextEventAsync(long afterSequence, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (status != "active" || latestEventSequence > afterSequence)
                {
                    return Task.CompletedTask;
                }

                return eventSignal.Task.WaitAsync(cancellationToken);
            }
        }

        public DirectConnectSessionState Close()
        {
            lock (gate)
            {
                status = "closed";
                eventSignal.TrySetResult(null);
                lastActivityAtUtc = DateTime.UtcNow;
                return new DirectConnectSessionState
                {
                    DirectConnectSessionId = directConnectSessionId,
                    BoundSessionId = boundSessionId,
                    WorkingDirectory = workingDirectory,
                    Status = status,
                    CreatedAtUtc = createdAtUtc,
                    LastActivityAtUtc = lastActivityAtUtc,
                    LatestEventSequence = latestEventSequence
                };
            }
        }

        private static TaskCompletionSource<object?> CreateEventSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
