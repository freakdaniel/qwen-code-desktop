using Microsoft.Extensions.Options;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Session Projection Service
/// </summary>
public sealed class SessionProjectionService : IDesktopSessionProjectionService, ISessionEventPublisher
{
    private readonly DesktopShellOptions _options;
    private readonly IWorkspacePathResolver _workspacePathResolver;
    private readonly ITranscriptStore _transcriptStore;
    private readonly ISessionService _sessionService;
    private readonly IToolExecutor _toolExecutor;
    private readonly ISessionHost _sessionHost;
    private readonly IActiveTurnRegistry _activeTurnRegistry;
    private EventHandler<DesktopSessionEvent>? _sessionEvent;

    /// <summary>
    /// Initializes a new instance of <see cref="SessionProjectionService"/>
    /// </summary>
    /// <param name="options">The options</param>
    /// <param name="workspacePathResolver">The workspace path resolver</param>
    /// <param name="transcriptStore">The transcript store</param>
    /// <param name="sessionService">The session service</param>
    /// <param name="toolExecutor">The tool executor</param>
    /// <param name="sessionHost">The session host</param>
    /// <param name="activeTurnRegistry">The active turn registry</param>
    public SessionProjectionService(
        IOptions<DesktopShellOptions> options,
        IWorkspacePathResolver workspacePathResolver,
        ITranscriptStore transcriptStore,
        ISessionService sessionService,
        IToolExecutor toolExecutor,
        ISessionHost sessionHost,
        IActiveTurnRegistry activeTurnRegistry)
    {
        _options = options.Value;
        _workspacePathResolver = workspacePathResolver;
        _transcriptStore = transcriptStore;
        _sessionService = sessionService;
        _toolExecutor = toolExecutor;
        _sessionHost = sessionHost;
        _activeTurnRegistry = activeTurnRegistry;

        _sessionHost.SessionEvent += (s, e) => _sessionEvent?.Invoke(s, e);
    }

    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    public event EventHandler<DesktopSessionEvent>? SessionEvent
    {
        add => _sessionEvent += value;
        remove => _sessionEvent -= value;
    }

    /// <inheritdoc/>
    public void Publish(DesktopSessionEvent sessionEvent) =>
        _sessionEvent?.Invoke(this, sessionEvent);

    /// <summary>
    /// Gets active turns async
    /// </summary>
    /// <returns>A task that resolves to i read only list active turn state</returns>
    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() =>
        Task.FromResult(_activeTurnRegistry.ListActiveTurns());

    /// <summary>
    /// Gets session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session detail?</returns>
    public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
        Task.FromResult(_transcriptStore.GetSession(ResolveWorkspace(), request));

    /// <summary>
    /// Removes session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to remove desktop session result</returns>
    public Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request)
    {
        var workspace = ResolveWorkspace();
        var removed = _sessionService.RemoveSession(workspace, request.SessionId);
        return Task.FromResult(new RemoveDesktopSessionResult
        {
            Removed = removed,
            SessionId = request.SessionId,
            RecentSessions = _transcriptStore.ListSessions(workspace)
        });
    }

    /// <summary>
    /// Executes native tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
        _toolExecutor.ExecuteAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Starts session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request) =>
        _sessionHost.StartTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) =>
        _sessionHost.ApprovePendingToolAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) =>
        _sessionHost.AnswerPendingQuestionAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Cancels session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
        _sessionHost.CancelTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
        _sessionHost.ResumeInterruptedTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
        _sessionHost.DismissInterruptedTurnAsync(ResolveWorkspace(), request);

    private WorkspacePaths ResolveWorkspace() => _workspacePathResolver.Resolve(_options.Workspace);
}
