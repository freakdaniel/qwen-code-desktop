using QwenCode.Core.Runtime;

namespace QwenCode.Core.Sessions;

/// <summary>
/// Defines the contract for Session Transcript Writer
/// </summary>
public interface ISessionTranscriptWriter
{
    /// <summary>
    /// Executes append entry async
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task AppendEntryAsync(string transcriptPath, object payload, CancellationToken cancellationToken);

    /// <summary>
    /// Executes mark tool entry resolved async
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="entryId">The entry id</param>
    /// <param name="resolutionStatus">The resolution status</param>
    /// <param name="resolvedAtUtc">The resolved at utc</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task MarkToolEntryResolvedAsync(
        string transcriptPath,
        string entryId,
        string resolutionStatus,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to read last entry uuid
    /// </summary>
    /// <param name="transcriptPath">The transcript path</param>
    /// <returns>The resulting string?</returns>
    string? TryReadLastEntryUuid(string transcriptPath);

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
    Task<string?> AppendAssistantToolExecutionsAsync(
        string transcriptPath,
        string sessionId,
        string? parentUuid,
        string gitBranch,
        IReadOnlyList<AssistantToolCallResult> toolExecutions,
        CancellationToken cancellationToken);
}
