using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

public interface IUserQuestionToolService
{
    IReadOnlyList<DesktopQuestionPrompt> ParseQuestions(JsonElement arguments);

    IReadOnlyList<DesktopQuestionPrompt> ParseQuestions(string? argumentsJson);

    string FormatAnswers(IReadOnlyList<DesktopQuestionPrompt> questions, IReadOnlyList<DesktopQuestionAnswer> answers);

    NativeToolExecutionResult CreatePendingResult(
        QwenRuntimeProfile runtimeProfile,
        string workingDirectory,
        JsonElement arguments,
        string approvalState);

    NativeToolExecutionResult CreateAnsweredResult(
        string workingDirectory,
        string approvalState,
        IReadOnlyList<DesktopQuestionPrompt> questions,
        IReadOnlyList<DesktopQuestionAnswer> answers);

    IReadOnlyList<DesktopQuestionAnswer> ValidateAnswers(
        IReadOnlyList<DesktopQuestionPrompt> questions,
        IReadOnlyList<DesktopQuestionAnswer> answers);
}
