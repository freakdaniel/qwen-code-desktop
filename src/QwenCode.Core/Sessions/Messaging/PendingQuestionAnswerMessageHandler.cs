using QwenCode.App.Compatibility;
using QwenCode.App.Models;
using QwenCode.App.Tools;

namespace QwenCode.App.Sessions;

/// <summary>
/// Represents the Pending Question Answer Message Handler
/// </summary>
/// <param name="transcriptStore">The transcript store</param>
/// <param name="pendingApprovalResolver">The pending approval resolver</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="userQuestionToolService">The user question tool service</param>
public sealed class PendingQuestionAnswerMessageHandler(
    ITranscriptStore transcriptStore,
    IPendingApprovalResolver pendingApprovalResolver,
    QwenRuntimeProfileService runtimeProfileService,
    IUserQuestionToolService userQuestionToolService)
{
    /// <summary>
    /// Executes handle async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to pending question answer message response</returns>
    public Task<PendingQuestionAnswerMessageResponse> HandleAsync(
        PendingQuestionAnswerMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to answer a pending question.");
        }

        var detail = transcriptStore.GetSession(request.Paths, new GetDesktopSessionRequest
        {
            SessionId = request.SessionId
        })
            ?? throw new InvalidOperationException("Session transcript was not found.");
        var pendingQuestion = pendingApprovalResolver.ResolvePendingQuestion(detail, request.EntryId);
        var questions = pendingQuestion.Questions.Count > 0
            ? pendingQuestion.Questions
            : userQuestionToolService.ParseQuestions(pendingQuestion.Arguments);
        var answers = userQuestionToolService.ValidateAnswers(questions, request.Answers);
        var workingDirectory = string.IsNullOrWhiteSpace(pendingQuestion.WorkingDirectory)
            ? runtimeProfileService.Inspect(request.Paths).ProjectRoot
            : pendingQuestion.WorkingDirectory;

        return Task.FromResult(new PendingQuestionAnswerMessageResponse
        {
            CorrelationId = request.CorrelationId,
            Detail = detail,
            PendingQuestion = pendingQuestion,
            WorkingDirectory = workingDirectory,
            GitBranch = pendingQuestion.GitBranch,
            Questions = questions,
            Answers = answers
        });
    }
}
