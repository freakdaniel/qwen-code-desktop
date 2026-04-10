using System.Text.Json;
using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

internal static class TodoStore
{
    private static readonly HashSet<string> AllowedStatuses =
    [
        "pending",
        "in_progress",
        "completed"
    ];

    /// <summary>
    /// Resolves todo file path
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>The resulting string</returns>
    public static string ResolveTodoFilePath(QwenRuntimeProfile runtimeProfile, string? sessionId)
    {
        var effectiveSessionId = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim();
        return Path.Combine(runtimeProfile.RuntimeBaseDirectory, "todos", $"{effectiveSessionId}.json");
    }

    /// <summary>
    /// Saves todos async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to (string file path, string summary)</returns>
    public static async Task<(string FilePath, string Summary)> SaveTodosAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("todos", out var todosElement) || todosElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Parameter 'todos' must be an array.");
        }

        var todos = ParseTodos(todosElement);
        var sessionId = TryGetString(arguments, "session_id") ?? TryGetString(arguments, "sessionId");
        var targetPath = ResolveTodoFilePath(runtimeProfile, sessionId);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using var stream = File.Create(targetPath);
        await JsonSerializer.SerializeAsync(
            stream,
            new
            {
                sessionId = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId,
                todos
            },
            cancellationToken: cancellationToken);

        var summary = BuildSummary(todos);
        return (targetPath, summary);
    }

    private static IReadOnlyList<TodoItemRecord> ParseTodos(JsonElement todosElement)
    {
        var todos = new List<TodoItemRecord>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var todoElement in todosElement.EnumerateArray())
        {
            if (todoElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Each todo must be an object.");
            }

            var id = TryGetString(todoElement, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("Each todo must include a non-empty 'id'.");
            }

            if (!ids.Add(id))
            {
                throw new InvalidOperationException("Todo ids must be unique within the array.");
            }

            var content = TryGetString(todoElement, "content");
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("Each todo must include a non-empty 'content'.");
            }

            var status = TryGetString(todoElement, "status");
            if (string.IsNullOrWhiteSpace(status) || !AllowedStatuses.Contains(status))
            {
                throw new InvalidOperationException("Each todo must include a valid 'status'.");
            }

            todos.Add(new TodoItemRecord
            {
                Id = id,
                Content = content,
                Status = status
            });
        }

        return todos;
    }

    private static string BuildSummary(IReadOnlyList<TodoItemRecord> todos)
    {
        if (todos.Count == 0)
        {
            return "Todo list cleared.";
        }

        var pendingCount = todos.Count(static todo => todo.Status == "pending");
        var inProgressCount = todos.Count(static todo => todo.Status == "in_progress");
        var completedCount = todos.Count(static todo => todo.Status == "completed");

        return $"Saved {todos.Count} todo item(s): pending {pendingCount}, in_progress {inProgressCount}, completed {completedCount}.";
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private sealed class TodoItemRecord
    {
        /// <summary>
        /// Gets or sets the id
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets or sets the content
        /// </summary>
        public required string Content { get; init; }

        /// <summary>
        /// Gets or sets the status
        /// </summary>
        public required string Status { get; init; }
    }
}
