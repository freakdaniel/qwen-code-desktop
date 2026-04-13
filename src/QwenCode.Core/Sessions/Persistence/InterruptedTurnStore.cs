using QwenCode.Core.Models;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Represents the Interrupted Turn Store
/// </summary>
public sealed class InterruptedTurnStore : IInterruptedTurnStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Executes upsert
    /// </summary>
    /// <param name="state">The state</param>
    public void Upsert(ActiveTurnState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.TranscriptPath);

        var chatsDirectory = Path.GetDirectoryName(state.TranscriptPath);
        if (string.IsNullOrWhiteSpace(chatsDirectory))
        {
            throw new InvalidOperationException("TranscriptPath must resolve to a chats directory.");
        }

        Directory.CreateDirectory(chatsDirectory);
        WriteRecord(GetStorePath(chatsDirectory, state.SessionId), new PersistedInterruptedTurnRecord
        {
            SessionId = state.SessionId,
            Prompt = state.Prompt,
            TranscriptPath = state.TranscriptPath,
            WorkingDirectory = state.WorkingDirectory,
            GitBranch = state.GitBranch,
            Status = state.Status,
            LastUpdatedAtUtc = state.LastUpdatedAtUtc,
            ContentSnapshot = state.ContentSnapshot,
            ThinkingSnapshot = state.ThinkingSnapshot,
            ToolName = state.ToolName,
            MarkerWritten = false
        });
    }

    /// <summary>
    /// Gets value
    /// </summary>
    /// <param name="chatsDirectory">The chats directory</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>The resulting interrupted turn state?</returns>
    public InterruptedTurnState? Get(string chatsDirectory, string sessionId)
    {
        var record = LoadRecord(chatsDirectory, sessionId);
        if (record is null)
        {
            return null;
        }

        return MaterializeInterruptedState(chatsDirectory, record);
    }

    /// <summary>
    /// Lists recoverable turns
    /// </summary>
    /// <param name="chatsDirectory">The chats directory</param>
    /// <returns>The resulting i read only list recoverable turn state</returns>
    public IReadOnlyList<RecoverableTurnState> ListRecoverableTurns(string chatsDirectory)
    {
        if (!Directory.Exists(chatsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(chatsDirectory, "*.interrupted.json")
            .Select(TryLoadRecordFromPath)
            .OfType<PersistedInterruptedTurnRecord>()
            .Select(record => MaterializeInterruptedState(chatsDirectory, record))
            .Where(static state => state is not null)
            .Select(static state => new RecoverableTurnState
            {
                SessionId = state!.SessionId,
                Prompt = state.Prompt,
                WorkingDirectory = state.WorkingDirectory,
                GitBranch = state.GitBranch,
                RecoveryReason = BuildRecoveryReason(state),
                LastUpdatedAtUtc = state.LastUpdatedAtUtc,
                ContentSnapshot = state.ContentSnapshot,
                ToolName = state.ToolName
            })
            .OrderByDescending(static state => state.LastUpdatedAtUtc)
            .ToArray();
    }

    /// <summary>
    /// Removes value
    /// </summary>
    /// <param name="chatsDirectory">The chats directory</param>
    /// <param name="sessionId">The session identifier</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool Remove(string chatsDirectory, string sessionId)
    {
        var storePath = GetStorePath(chatsDirectory, sessionId);
        if (!File.Exists(storePath))
        {
            return false;
        }

        File.Delete(storePath);
        return true;
    }

    private static string BuildRecoveryReason(InterruptedTurnState state)
    {
        if (!string.IsNullOrWhiteSpace(state.ToolName))
        {
            return $"The previous turn stopped while working with '{state.ToolName}'.";
        }

        return "The previous turn stopped before the assistant finished responding.";
    }

    private static InterruptedTurnState? MaterializeInterruptedState(
        string chatsDirectory,
        PersistedInterruptedTurnRecord record)
    {
        var transcriptPath = string.IsNullOrWhiteSpace(record.TranscriptPath)
            ? Path.Combine(chatsDirectory, $"{record.SessionId}.jsonl")
            : record.TranscriptPath;

        if (!record.MarkerWritten)
        {
            AppendInterruptedMarker(record with { TranscriptPath = transcriptPath });
            record = record with { TranscriptPath = transcriptPath, MarkerWritten = true };
            WriteRecord(GetStorePath(chatsDirectory, record.SessionId), record);
        }

        return new InterruptedTurnState
        {
            SessionId = record.SessionId,
            Prompt = record.Prompt,
            TranscriptPath = transcriptPath,
            WorkingDirectory = record.WorkingDirectory,
            GitBranch = record.GitBranch,
            Status = record.Status,
            InterruptedAtUtc = record.LastUpdatedAtUtc,
            LastUpdatedAtUtc = record.LastUpdatedAtUtc,
            ContentSnapshot = record.ContentSnapshot,
            ThinkingSnapshot = record.ThinkingSnapshot,
            ToolName = record.ToolName
        };
    }

    private static void AppendInterruptedMarker(PersistedInterruptedTurnRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.TranscriptPath))
        {
            return;
        }

        var transcriptDirectory = Path.GetDirectoryName(record.TranscriptPath);
        if (string.IsNullOrWhiteSpace(transcriptDirectory))
        {
            return;
        }

        Directory.CreateDirectory(transcriptDirectory);
        var parentUuid = TryReadLastEntryUuid(record.TranscriptPath);
        var payload = new
        {
            uuid = Guid.NewGuid().ToString(),
            parentUuid,
            sessionId = record.SessionId,
            timestamp = DateTime.UtcNow,
            type = "system",
            cwd = record.WorkingDirectory,
            version = "0.1.0",
            gitBranch = record.GitBranch,
            status = "interrupted",
            messageText = BuildInterruptedMarkerMessage(record)
        };

        File.AppendAllText(
            record.TranscriptPath,
            JsonSerializer.Serialize(payload) + Environment.NewLine);
    }

    private static string BuildInterruptedMarkerMessage(PersistedInterruptedTurnRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.ContentSnapshot))
        {
            return $"The previous desktop turn was interrupted. Last assistant snapshot: {record.ContentSnapshot}";
        }

        return "The previous desktop turn was interrupted before it completed.";
    }

    private static string? TryReadLastEntryUuid(string transcriptPath)
    {
        if (!File.Exists(transcriptPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(transcriptPath).Reverse())
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("uuid", out var uuidProperty) &&
                    uuidProperty.ValueKind == JsonValueKind.String)
                {
                    return uuidProperty.GetString();
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static PersistedInterruptedTurnRecord? LoadRecord(string chatsDirectory, string sessionId) =>
        TryLoadRecordFromPath(GetStorePath(chatsDirectory, sessionId));

    private static PersistedInterruptedTurnRecord? TryLoadRecordFromPath(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PersistedInterruptedTurnRecord>(File.ReadAllText(path), SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteRecord(string path, PersistedInterruptedTurnRecord record)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(record, SerializerOptions));
    }

    private static string GetStorePath(string chatsDirectory, string sessionId) =>
        Path.Combine(chatsDirectory, $"{sessionId}.interrupted.json");

    private sealed record PersistedInterruptedTurnRecord
    {
        /// <summary>
        /// Gets or sets the session id
        /// </summary>
        public required string SessionId { get; init; }

        /// <summary>
        /// Gets or sets the prompt
        /// </summary>
        public required string Prompt { get; init; }

        /// <summary>
        /// Gets or sets the transcript path
        /// </summary>
        public required string TranscriptPath { get; init; }

        /// <summary>
        /// Gets or sets the working directory
        /// </summary>
        public required string WorkingDirectory { get; init; }

        /// <summary>
        /// Gets or sets the git branch
        /// </summary>
        public required string GitBranch { get; init; }

        /// <summary>
        /// Gets or sets the status
        /// </summary>
        public required string Status { get; init; }

        /// <summary>
        /// Gets or sets the last updated at utc
        /// </summary>
        public required DateTime LastUpdatedAtUtc { get; init; }

        /// <summary>
        /// Gets or sets the content snapshot
        /// </summary>
        public string ContentSnapshot { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the thinking snapshot
        /// </summary>
        public string ThinkingSnapshot { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the tool name
        /// </summary>
        public string ToolName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the marker written
        /// </summary>
        public bool MarkerWritten { get; init; }
    }
}
