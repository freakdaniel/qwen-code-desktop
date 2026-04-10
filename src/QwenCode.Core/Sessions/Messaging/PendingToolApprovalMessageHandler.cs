using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Represents the Pending Tool Approval Message Handler
/// </summary>
/// <param name="transcriptStore">The transcript store</param>
/// <param name="pendingApprovalResolver">The pending approval resolver</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
public sealed class PendingToolApprovalMessageHandler(
    ITranscriptStore transcriptStore,
    IPendingApprovalResolver pendingApprovalResolver,
    QwenRuntimeProfileService runtimeProfileService)
{
    /// <summary>
    /// Executes handle async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to pending tool approval message response</returns>
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
