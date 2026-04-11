using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Defines the contract for User Question Tool Service
/// </summary>
public interface IUserQuestionToolService
{
    /// <summary>
    /// Executes parse questions
    /// </summary>
    /// <param name="arguments">The arguments</param>
    /// <returns>The resulting i read only list desktop question prompt</returns>
    IReadOnlyList<DesktopQuestionPrompt> ParseQuestions(JsonElement arguments);

    /// <summary>
    /// Executes parse questions
    /// </summary>
    /// <param name="argumentsJson">The arguments json</param>
    /// <returns>The resulting i read only list desktop question prompt</returns>
    IReadOnlyList<DesktopQuestionPrompt> ParseQuestions(string? argumentsJson);

    /// <summary>
    /// Executes format answers
    /// </summary>
    /// <param name="questions">The questions</param>
    /// <param name="answers">The answers</param>
    /// <returns>The resulting string</returns>
    string FormatAnswers(IReadOnlyList<DesktopQuestionPrompt> questions, IReadOnlyList<DesktopQuestionAnswer> answers);

    /// <summary>
    /// Creates pending result
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="approvalState">The approval state</param>
    /// <returns>The resulting native tool execution result</returns>
    NativeToolExecutionResult CreatePendingResult(
        QwenRuntimeProfile runtimeProfile,
        string workingDirectory,
        JsonElement arguments,
        string approvalState);

    /// <summary>
    /// Creates answered result
    /// </summary>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="approvalState">The approval state</param>
    /// <param name="questions">The questions</param>
    /// <param name="answers">The answers</param>
    /// <returns>The resulting native tool execution result</returns>
    NativeToolExecutionResult CreateAnsweredResult(
        string workingDirectory,
        string approvalState,
        IReadOnlyList<DesktopQuestionPrompt> questions,
        IReadOnlyList<DesktopQuestionAnswer> answers);

    /// <summary>
    /// Validates answers
    /// </summary>
    /// <param name="questions">The questions</param>
    /// <param name="answers">The answers</param>
    /// <returns>The resulting i read only list desktop question answer</returns>
    IReadOnlyList<DesktopQuestionAnswer> ValidateAnswers(
        IReadOnlyList<DesktopQuestionPrompt> questions,
        IReadOnlyList<DesktopQuestionAnswer> answers);
}
