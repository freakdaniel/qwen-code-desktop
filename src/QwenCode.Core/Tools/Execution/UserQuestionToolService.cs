using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Tools;

/// <summary>
/// Represents the User Question Tool Service
/// </summary>
public sealed class UserQuestionToolService : IUserQuestionToolService
{
    private const int MaximumQuestionCount = 4;
    private const int MaximumOptionCount = 4;

    /// <summary>
    /// Executes parse questions
    /// </summary>
    /// <param name="arguments">The arguments</param>
    /// <returns>The resulting i read only list desktop question prompt</returns>
    public IReadOnlyList<DesktopQuestionPrompt> ParseQuestions(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("questions", out var questionsElement) ||
            questionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Parameter 'questions' is required.");
        }

        var questions = new List<DesktopQuestionPrompt>();
        foreach (var questionElement in questionsElement.EnumerateArray())
        {
            var prompt = ParseQuestion(questionElement);
            questions.Add(prompt);
        }

        if (questions.Count == 0)
        {
            throw new InvalidOperationException("At least one question is required.");
        }

        if (questions.Count > MaximumQuestionCount)
        {
            throw new InvalidOperationException($"At most {MaximumQuestionCount} questions are supported.");
        }

        return questions;
    }

    /// <summary>
    /// Executes parse questions
    /// </summary>
    /// <param name="argumentsJson">The arguments json</param>
    /// <returns>The resulting i read only list desktop question prompt</returns>
    public IReadOnlyList<DesktopQuestionPrompt> ParseQuestions(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return ParseQuestions(document.RootElement);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Executes format answers
    /// </summary>
    /// <param name="questions">The questions</param>
    /// <param name="answers">The answers</param>
    /// <returns>The resulting string</returns>
    public string FormatAnswers(IReadOnlyList<DesktopQuestionPrompt> questions, IReadOnlyList<DesktopQuestionAnswer> answers)
    {
        var normalizedAnswers = ValidateAnswers(questions, answers);
        var lines = normalizedAnswers
            .OrderBy(static answer => answer.QuestionIndex)
            .Select(answer =>
            {
                var question = questions[answer.QuestionIndex];
                var header = string.IsNullOrWhiteSpace(question.Header)
                    ? $"Question {answer.QuestionIndex + 1}"
                    : question.Header;
                return $"**{header}**: {answer.Value}";
            });

        return $"User has provided the following answers:{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    /// <summary>
    /// Creates pending result
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="approvalState">The approval state</param>
    /// <returns>The resulting native tool execution result</returns>
    public NativeToolExecutionResult CreatePendingResult(
        QwenRuntimeProfile runtimeProfile,
        string workingDirectory,
        JsonElement arguments,
        string approvalState)
    {
        var questions = ParseQuestions(arguments);
        return new NativeToolExecutionResult
        {
            ToolName = "ask_user_question",
            Status = "input-required",
            ApprovalState = string.IsNullOrWhiteSpace(approvalState) ? "ask" : approvalState,
            WorkingDirectory = workingDirectory,
            Output = BuildPendingMessage(questions),
            ChangedFiles = [],
            Questions = questions
        };
    }

    /// <summary>
    /// Creates answered result
    /// </summary>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="approvalState">The approval state</param>
    /// <param name="questions">The questions</param>
    /// <param name="answers">The answers</param>
    /// <returns>The resulting native tool execution result</returns>
    public NativeToolExecutionResult CreateAnsweredResult(
        string workingDirectory,
        string approvalState,
        IReadOnlyList<DesktopQuestionPrompt> questions,
        IReadOnlyList<DesktopQuestionAnswer> answers)
    {
        var normalizedAnswers = ValidateAnswers(questions, answers);
        return new NativeToolExecutionResult
        {
            ToolName = "ask_user_question",
            Status = "completed",
            ApprovalState = string.IsNullOrWhiteSpace(approvalState) ? "ask" : approvalState,
            WorkingDirectory = workingDirectory,
            Output = FormatAnswers(questions, normalizedAnswers),
            ChangedFiles = [],
            Questions = questions,
            Answers = normalizedAnswers
        };
    }

    /// <summary>
    /// Validates answers
    /// </summary>
    /// <param name="questions">The questions</param>
    /// <param name="answers">The answers</param>
    /// <returns>The resulting i read only list desktop question answer</returns>
    public IReadOnlyList<DesktopQuestionAnswer> ValidateAnswers(
        IReadOnlyList<DesktopQuestionPrompt> questions,
        IReadOnlyList<DesktopQuestionAnswer> answers)
    {
        if (questions.Count == 0)
        {
            throw new InvalidOperationException("No pending questions were found.");
        }

        var normalizedAnswers = answers
            .Where(answer => !string.IsNullOrWhiteSpace(answer.Value))
            .GroupBy(answer => answer.QuestionIndex)
            .Select(group => group.Last())
            .OrderBy(static answer => answer.QuestionIndex)
            .ToArray();

        if (normalizedAnswers.Length != questions.Count)
        {
            throw new InvalidOperationException("All pending questions must be answered before the session can continue.");
        }

        for (var index = 0; index < normalizedAnswers.Length; index++)
        {
            var answer = normalizedAnswers[index];
            if (answer.QuestionIndex < 0 || answer.QuestionIndex >= questions.Count)
            {
                throw new InvalidOperationException("Question answers include an out-of-range question index.");
            }

            if (string.IsNullOrWhiteSpace(answer.Value))
            {
                throw new InvalidOperationException("Question answers cannot be empty.");
            }
        }

        return normalizedAnswers;
    }

    private static DesktopQuestionPrompt ParseQuestion(JsonElement questionElement)
    {
        var header = TryGetRequiredString(questionElement, "header");
        var question = TryGetRequiredString(questionElement, "question");
        var multiSelect = questionElement.TryGetProperty("multiSelect", out var multiSelectElement) &&
            multiSelectElement.ValueKind == JsonValueKind.True;

        if (!questionElement.TryGetProperty("options", out var optionsElement) ||
            optionsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Each question requires an 'options' array.");
        }

        var options = new List<DesktopQuestionOption>();
        foreach (var optionElement in optionsElement.EnumerateArray())
        {
            options.Add(new DesktopQuestionOption
            {
                Label = TryGetRequiredString(optionElement, "label"),
                Description = TryGetRequiredString(optionElement, "description")
            });
        }

        if (options.Count < 2 || options.Count > MaximumOptionCount)
        {
            throw new InvalidOperationException($"Each question requires between 2 and {MaximumOptionCount} options.");
        }

        return new DesktopQuestionPrompt
        {
            Header = header,
            Question = question,
            MultiSelect = multiSelect,
            Options = options
        };
    }

    private static string BuildPendingMessage(IReadOnlyList<DesktopQuestionPrompt> questions)
    {
        var lines = questions.Select(question =>
            $"- {question.Header}: {question.Question}");
        return $"Waiting for user answers to {questions.Count} question(s).{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private static string TryGetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException($"Property '{propertyName}' is required.");
        }

        return property.GetString()!;
    }
}
