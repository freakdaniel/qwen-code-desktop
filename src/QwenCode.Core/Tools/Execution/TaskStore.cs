using System.Text.Json;

namespace QwenCode.Core.Tools;

internal static class TaskStore
{
    private static readonly HashSet<string> AllowedStatuses =
    [
        "pending",
        "in_progress",
        "completed",
        "cancelled"
    ];

    public static string ResolveTaskFilePath(QwenCode.Core.Models.QwenRuntimeProfile runtimeProfile, string? sessionId)
    {
        var effectiveSessionId = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim();
        return Path.Combine(runtimeProfile.RuntimeBaseDirectory, "tasks", $"{effectiveSessionId}.json");
    }

    public static async Task<TaskCreateResult> CreateTaskAsync(
        QwenCode.Core.Models.QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var subject = RequireString(arguments, "subject");
        var description = RequireString(arguments, "description");
        var sessionId = TryGetString(arguments, "session_id") ?? TryGetString(arguments, "sessionId");
        var activeForm = TryGetString(arguments, "active_form") ?? TryGetString(arguments, "activeForm") ?? string.Empty;
        var owner = TryGetString(arguments, "owner") ?? string.Empty;
        var metadata = TryGetJsonElement(arguments, "metadata");

        var filePath = ResolveTaskFilePath(runtimeProfile, sessionId);
        var document = await LoadDocumentAsync(filePath, sessionId, cancellationToken);
        var taskId = AllocateTaskId(document.Tasks);
        var now = DateTime.UtcNow;
        var task = new TaskRecord
        {
            Id = taskId,
            Subject = subject,
            Description = description,
            ActiveForm = activeForm,
            Status = "pending",
            Owner = owner,
            Blocks = [],
            BlockedBy = [],
            Metadata = metadata,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        document.Tasks.Add(task);
        await SaveDocumentAsync(filePath, document, cancellationToken);

        return new TaskCreateResult(filePath, task);
    }

    public static async Task<TaskListResult> ListTasksAsync(
        QwenCode.Core.Models.QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var sessionId = TryGetString(arguments, "session_id") ?? TryGetString(arguments, "sessionId");
        var filePath = ResolveTaskFilePath(runtimeProfile, sessionId);
        var document = await LoadDocumentAsync(filePath, sessionId, cancellationToken);
        return new TaskListResult(filePath, document.Tasks.OrderBy(static item => ParseSortableId(item.Id)).ThenBy(static item => item.Id, StringComparer.Ordinal).ToArray());
    }

    public static async Task<TaskRecord?> GetTaskAsync(
        QwenCode.Core.Models.QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var taskId = RequireString(arguments, "task_id", "taskId");
        var sessionId = TryGetString(arguments, "session_id") ?? TryGetString(arguments, "sessionId");
        var filePath = ResolveTaskFilePath(runtimeProfile, sessionId);
        var document = await LoadDocumentAsync(filePath, sessionId, cancellationToken);
        return document.Tasks.FirstOrDefault(task => string.Equals(task.Id, taskId, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<TaskUpdateResult> UpdateTaskAsync(
        QwenCode.Core.Models.QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var taskId = RequireString(arguments, "task_id", "taskId");
        var sessionId = TryGetString(arguments, "session_id") ?? TryGetString(arguments, "sessionId");
        var filePath = ResolveTaskFilePath(runtimeProfile, sessionId);
        var document = await LoadDocumentAsync(filePath, sessionId, cancellationToken);
        var task = document.Tasks.FirstOrDefault(item => string.Equals(item.Id, taskId, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            return new TaskUpdateResult(filePath, null, []);
        }

        var updatedFields = new List<string>();

        ApplyStringUpdate(arguments, "subject", value => task.Subject = value, task.Subject, updatedFields);
        ApplyStringUpdate(arguments, "description", value => task.Description = value, task.Description, updatedFields);
        ApplyStringUpdate(arguments, "active_form", value => task.ActiveForm = value, task.ActiveForm, updatedFields);
        ApplyStringUpdate(arguments, "activeForm", value => task.ActiveForm = value, task.ActiveForm, updatedFields);
        ApplyStringUpdate(arguments, "owner", value => task.Owner = value, task.Owner, updatedFields);

        var status = TryGetString(arguments, "status");
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!AllowedStatuses.Contains(status))
            {
                throw new InvalidOperationException("Parameter 'status' must be one of: pending, in_progress, completed, cancelled.");
            }

            if (!string.Equals(task.Status, status, StringComparison.Ordinal))
            {
                task.Status = status;
                updatedFields.Add("status");
            }
        }

        if (arguments.TryGetProperty("add_blocks", out var addBlocksElement))
        {
            MergeStringList(task.Blocks, ParseStringArray(addBlocksElement, "add_blocks"), updatedFields, "blocks");
        }

        if (arguments.TryGetProperty("add_blocked_by", out var addBlockedByElement))
        {
            MergeStringList(task.BlockedBy, ParseStringArray(addBlockedByElement, "add_blocked_by"), updatedFields, "blockedBy");
        }

        if (TryGetJsonElement(arguments, "metadata") is { } metadata)
        {
            task.Metadata = metadata;
            updatedFields.Add("metadata");
        }

        if (updatedFields.Count == 0)
        {
            return new TaskUpdateResult(filePath, task, []);
        }

        task.UpdatedAtUtc = DateTime.UtcNow;
        await SaveDocumentAsync(filePath, document, cancellationToken);
        return new TaskUpdateResult(filePath, task, updatedFields);
    }

    public static async Task<TaskStopResult> StopTaskAsync(
        QwenCode.Core.Models.QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var taskId = RequireString(arguments, "task_id", "taskId");
        var sessionId = TryGetString(arguments, "session_id") ?? TryGetString(arguments, "sessionId");
        var filePath = ResolveTaskFilePath(runtimeProfile, sessionId);
        var document = await LoadDocumentAsync(filePath, sessionId, cancellationToken);
        var task = document.Tasks.FirstOrDefault(item => string.Equals(item.Id, taskId, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            return new TaskStopResult(filePath, null);
        }

        task.Status = "cancelled";
        task.UpdatedAtUtc = DateTime.UtcNow;
        await SaveDocumentAsync(filePath, document, cancellationToken);
        return new TaskStopResult(filePath, task);
    }

    private static async Task<TaskDocument> LoadDocumentAsync(
        string filePath,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new TaskDocument
            {
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim(),
                Tasks = []
            };
        }

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<TaskDocument>(stream, cancellationToken: cancellationToken)
               ?? new TaskDocument
               {
                   SessionId = string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim(),
                   Tasks = []
               };
    }

    private static async Task SaveDocumentAsync(string filePath, TaskDocument document, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, document, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private static string AllocateTaskId(IReadOnlyList<TaskRecord> tasks)
    {
        var next = tasks
            .Select(static task => ParseSortableId(task.Id))
            .DefaultIfEmpty(0)
            .Max() + 1;
        return next.ToString();
    }

    private static int ParseSortableId(string id) =>
        int.TryParse(id, out var numeric) ? numeric : 0;

    private static void ApplyStringUpdate(
        JsonElement arguments,
        string propertyName,
        Action<string> apply,
        string currentValue,
        List<string> updatedFields)
    {
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = property.GetString() ?? string.Empty;
        if (string.Equals(currentValue, value, StringComparison.Ordinal))
        {
            return;
        }

        apply(value);
        updatedFields.Add(propertyName);
    }

    private static void MergeStringList(
        List<string> target,
        IReadOnlyList<string> additions,
        List<string> updatedFields,
        string fieldName)
    {
        var changed = false;
        foreach (var value in additions)
        {
            if (target.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            target.Add(value);
            changed = true;
        }

        if (changed)
        {
            updatedFields.Add(fieldName);
        }
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Parameter '{propertyName}' must be an array.");
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"Parameter '{propertyName}' must contain only strings.");
            }

            var value = item.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static string RequireString(JsonElement arguments, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = TryGetString(arguments, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException($"Parameter '{propertyNames[0]}' is required.");
    }

    private static string? TryGetString(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static JsonElement? TryGetJsonElement(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) ? property.Clone() : null;

    internal sealed class TaskDocument
    {
        public required string SessionId { get; init; }

        public List<TaskRecord> Tasks { get; init; } = [];
    }

    internal sealed class TaskRecord
    {
        public required string Id { get; set; }

        public required string Subject { get; set; }

        public required string Description { get; set; }

        public string ActiveForm { get; set; } = string.Empty;

        public required string Status { get; set; }

        public string Owner { get; set; } = string.Empty;

        public List<string> Blocks { get; init; } = [];

        public List<string> BlockedBy { get; init; } = [];

        public JsonElement? Metadata { get; set; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; set; }
    }

    internal sealed record TaskCreateResult(string FilePath, TaskRecord Task);

    internal sealed record TaskListResult(string FilePath, IReadOnlyList<TaskRecord> Tasks);

    internal sealed record TaskUpdateResult(string FilePath, TaskRecord? Task, IReadOnlyList<string> UpdatedFields);

    internal sealed record TaskStopResult(string FilePath, TaskRecord? Task);
}
