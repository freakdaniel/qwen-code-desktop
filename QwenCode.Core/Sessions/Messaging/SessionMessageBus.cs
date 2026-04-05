using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public sealed class SessionMessageBus(
    PendingToolApprovalMessageHandler pendingToolApprovalMessageHandler,
    PendingQuestionAnswerMessageHandler pendingQuestionAnswerMessageHandler) : ISessionMessageBus
{
    public async Task<PendingToolApprovalMessageResponse> RequestPendingToolApprovalAsync(
        PendingToolApprovalMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = new PendingToolApprovalMessageRequest
        {
            CorrelationId = ResolveCorrelationId(request.CorrelationId),
            Paths = request.Paths,
            SessionId = request.SessionId,
            EntryId = request.EntryId
        };
        var response = await pendingToolApprovalMessageHandler.HandleAsync(normalizedRequest, cancellationToken);

        return ValidateCorrelation(normalizedRequest.CorrelationId, response);
    }

    public async Task<PendingQuestionAnswerMessageResponse> RequestPendingQuestionAnswerAsync(
        PendingQuestionAnswerMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = new PendingQuestionAnswerMessageRequest
        {
            CorrelationId = ResolveCorrelationId(request.CorrelationId),
            Paths = request.Paths,
            SessionId = request.SessionId,
            EntryId = request.EntryId,
            Answers = request.Answers
        };
        var response = await pendingQuestionAnswerMessageHandler.HandleAsync(normalizedRequest, cancellationToken);

        return ValidateCorrelation(normalizedRequest.CorrelationId, response);
    }

    private static string ResolveCorrelationId(string? correlationId) =>
        string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;

    private static PendingToolApprovalMessageResponse ValidateCorrelation(
        string expectedCorrelationId,
        PendingToolApprovalMessageResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.CorrelationId))
        {
            return new PendingToolApprovalMessageResponse
            {
                CorrelationId = expectedCorrelationId,
                Detail = response.Detail,
                PendingTool = response.PendingTool,
                WorkingDirectory = response.WorkingDirectory,
                GitBranch = response.GitBranch
            };
        }

        if (!string.Equals(expectedCorrelationId, response.CorrelationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Pending tool approval response correlation did not match the request.");
        }

        return response;
    }

    private static PendingQuestionAnswerMessageResponse ValidateCorrelation(
        string expectedCorrelationId,
        PendingQuestionAnswerMessageResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.CorrelationId))
        {
            return new PendingQuestionAnswerMessageResponse
            {
                CorrelationId = expectedCorrelationId,
                Detail = response.Detail,
                PendingQuestion = response.PendingQuestion,
                WorkingDirectory = response.WorkingDirectory,
                GitBranch = response.GitBranch,
                Questions = response.Questions,
                Answers = response.Answers
            };
        }

        if (!string.Equals(expectedCorrelationId, response.CorrelationId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Pending question response correlation did not match the request.");
        }

        return response;
    }
}
