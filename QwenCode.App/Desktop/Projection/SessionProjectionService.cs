using Microsoft.Extensions.Options;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;

namespace QwenCode.App.Desktop;

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

    public event EventHandler<DesktopSessionEvent>? SessionEvent
    {
        add => sessionHost.SessionEvent += value;
        remove => sessionHost.SessionEvent -= value;
    }

    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() =>
        Task.FromResult(activeTurnRegistry.ListActiveTurns());

    public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
        Task.FromResult(transcriptStore.GetSession(ResolveWorkspace(), request));

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

    public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
        toolExecutor.ExecuteAsync(ResolveWorkspace(), request);

    public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request) =>
        sessionHost.StartTurnAsync(ResolveWorkspace(), request);

    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) =>
        sessionHost.ApprovePendingToolAsync(ResolveWorkspace(), request);

    public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) =>
        sessionHost.AnswerPendingQuestionAsync(ResolveWorkspace(), request);

    public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
        sessionHost.CancelTurnAsync(ResolveWorkspace(), request);

    public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
        sessionHost.ResumeInterruptedTurnAsync(ResolveWorkspace(), request);

    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
        sessionHost.DismissInterruptedTurnAsync(ResolveWorkspace(), request);

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(_options.Workspace);
}
