using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public sealed class PendingToolApprovalMessageHandler(
    ITranscriptStore transcriptStore,
    IPendingApprovalResolver pendingApprovalResolver,
    QwenRuntimeProfileService runtimeProfileService)
{
    public Task<PendingToolApprovalMessageResponse> HandleAsync(
        PendingToolApprovalMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to approve a pending tool.");
        }

        var detail = transcriptStore.GetSession(request.Paths, new GetDesktopSessionRequest
        {
            SessionId = request.SessionId
        })
            ?? throw new InvalidOperationException("Session transcript was not found.");
        var pendingTool = pendingApprovalResolver.ResolvePendingTool(detail, request.EntryId);
        var workingDirectory = string.IsNullOrWhiteSpace(pendingTool.WorkingDirectory)
            ? runtimeProfileService.Inspect(request.Paths).ProjectRoot
            : pendingTool.WorkingDirectory;

        return Task.FromResult(new PendingToolApprovalMessageResponse
        {
            CorrelationId = request.CorrelationId,
            Detail = detail,
            PendingTool = pendingTool,
            WorkingDirectory = workingDirectory,
            GitBranch = pendingTool.GitBranch
        });
    }
}
