using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface ISessionMessageBus
{
    Task<PendingToolApprovalMessageResponse> RequestPendingToolApprovalAsync(
        PendingToolApprovalMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<PendingQuestionAnswerMessageResponse> RequestPendingQuestionAnswerAsync(
        PendingQuestionAnswerMessageRequest request,
        CancellationToken cancellationToken = default);
}
