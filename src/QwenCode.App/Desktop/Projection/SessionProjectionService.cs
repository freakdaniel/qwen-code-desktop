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
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="transcriptStore">The transcript store</param>
/// <param name="sessionService">The session service</param>
/// <param name="toolExecutor">The tool executor</param>
/// <param name="sessionHost">The session host</param>
/// <param name="activeTurnRegistry">The active turn registry</param>
public sealed class SessionProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    ITranscriptStore transcriptStore,
    ISessionService sessionService,
    IToolExecutor toolExecutor,
    ISessionHost sessionHost,
    IActiveTurnRegistry activeTurnRegistry) : IDesktopSessionProjectionService
{
    private readonly DesktopShellOptions _options = options.Value;

    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    public event EventHandler<DesktopSessionEvent>? SessionEvent
    {
        add => sessionHost.SessionEvent += value;
        remove => sessionHost.SessionEvent -= value;
    }

    /// <summary>
    /// Gets active turns async
    /// </summary>
    /// <returns>A task that resolves to i read only list active turn state</returns>
    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() =>
        Task.FromResult(activeTurnRegistry.ListActiveTurns());

    /// <summary>
    /// Gets session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session detail?</returns>
    public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
        Task.FromResult(transcriptStore.GetSession(ResolveWorkspace(), request));

    /// <summary>
    /// Removes session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to remove desktop session result</returns>
    public Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request)
    {
        var workspace = ResolveWorkspace();
        var removed = sessionService.RemoveSession(workspace, request.SessionId);
        return Task.FromResult(new RemoveDesktopSessionResult
        {
            Removed = removed,
            SessionId = request.SessionId,
            RecentSessions = transcriptStore.ListSessions(workspace)
        });
    }

    /// <summary>
    /// Executes native tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
        toolExecutor.ExecuteAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Starts session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request) =>
        sessionHost.StartTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) =>
        sessionHost.ApprovePendingToolAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) =>
        sessionHost.AnswerPendingQuestionAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Cancels session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
        sessionHost.CancelTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
        sessionHost.ResumeInterruptedTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
        sessionHost.DismissInterruptedTurnAsync(ResolveWorkspace(), request);

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(_options.Workspace);
}
