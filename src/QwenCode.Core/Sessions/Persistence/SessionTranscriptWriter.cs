using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

/// <summary>
/// Represents the Session Transcript Writer
/// </summary>
public sealed class SessionTranscriptWriter : ISessionTranscriptWriter
{
    /// <summary>
    /// Executes append entry async
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task AppendEntryAsync(string transcriptPath, object payload, CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(payload);
        await File.AppendAllTextAsync(
            transcriptPath,
            line + Environment.NewLine,
            cancellationToken);
    }

    /// <summary>
    /// Executes mark tool entry resolved async
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="entryId">The entry id</param>
    /// <param name="resolutionStatus">The resolution status</param>
    /// <param name="resolvedAtUtc">The resolved at utc</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task MarkToolEntryResolvedAsync(
        string transcriptPath,
        string entryId,
        string resolutionStatus,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(transcriptPath, cancellationToken);
        var updated = false;

        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            JsonObject? node;
            try
            {
                node = JsonNode.Parse(lines[index])?.AsObject();
            }
            catch
            {
                continue;
            }

            if (node is null ||
                !string.Equals(node["uuid"]?.GetValue<string>(), entryId, StringComparison.Ordinal))
            {
                continue;
            }

            node["resolutionStatus"] = resolutionStatus;
            node["resolvedAt"] = resolvedAtUtc;
            lines[index] = node.ToJsonString();
            updated = true;
            break;
        }

        if (updated)
        {
            await File.WriteAllLinesAsync(transcriptPath, lines, cancellationToken);
        }
    }

    /// <summary>
    /// Attempts to read last entry uuid
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <returns>The resulting string?</returns>
    public string? TryReadLastEntryUuid(string transcriptPath)
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

    /// <summary>
    /// Executes append assistant tool executions async
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="parentUuid">The parent uuid</param>
    /// <param name="gitBranch">The git branch</param>
    /// <param name="toolExecutions">The tool executions</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string?</returns>
    public async Task<string?> AppendAssistantToolExecutionsAsync(
        string transcriptPath,
        string sessionId,
        string? parentUuid,
        string gitBranch,
        IReadOnlyList<AssistantToolCallResult> toolExecutions,
        CancellationToken cancellationToken)
    {
        foreach (var toolExecution in toolExecutions)
        {
            var toolUuid = Guid.NewGuid().ToString();
            await AppendEntryAsync(
                transcriptPath,
                new
                {
                    uuid = toolUuid,
                    parentUuid,
                    sessionId,
                    timestamp = DateTime.UtcNow,
                    type = "tool",
                    cwd = toolExecution.Execution.WorkingDirectory,
                    version = "0.1.0",
                    gitBranch,
                    toolName = toolExecution.Execution.ToolName,
                    args = toolExecution.ToolCall.ArgumentsJson,
                    approvalState = toolExecution.Execution.ApprovalState,
                    status = toolExecution.Execution.Status,
                    output = toolExecution.Execution.Output,
                    errorMessage = toolExecution.Execution.ErrorMessage,
                    exitCode = toolExecution.Execution.ExitCode,
                    changedFiles = toolExecution.Execution.ChangedFiles,
                    questions = toolExecution.Execution.Questions,
                    answers = toolExecution.Execution.Answers,
                    source = "assistant-runtime"
                },
                cancellationToken);

            parentUuid = toolUuid;
        }

        return parentUuid;
    }
}
