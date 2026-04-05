using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Defines the contract for Session Message Bus
/// </summary>
public interface ISessionMessageBus
{
    /// <summary>
    /// Executes request pending tool approval async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to pending tool approval message response</returns>
    Task<PendingToolApprovalMessageResponse> RequestPendingToolApprovalAsync(
        PendingToolApprovalMessageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes request pending question answer async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to pending question answer message response</returns>
    Task<PendingQuestionAnswerMessageResponse> RequestPendingQuestionAnswerAsync(
        PendingQuestionAnswerMessageRequest request,
        CancellationToken cancellationToken = default);
}
