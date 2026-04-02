using QwenCode.App.Runtime;

namespace QwenCode.App.Sessions;

public interface ISessionTranscriptWriter
{
    Task AppendEntryAsync(string transcriptPath, object payload, CancellationToken cancellationToken);

    Task MarkToolEntryResolvedAsync(
        string transcriptPath,
        string entryId,
        string resolutionStatus,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken);

    string? TryReadLastEntryUuid(string transcriptPath);

    Task<string?> AppendAssistantToolExecutionsAsync(
        string transcriptPath,
        string sessionId,
        string? parentUuid,
        string gitBranch,
        IReadOnlyList<AssistantToolCallResult> toolExecutions,
        CancellationToken cancellationToken);
}
