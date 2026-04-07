namespace QwenCode.App.Desktop.Projection;

/// <summary>
/// Defines the contract for Session Title Generation Service
/// </summary>
public interface ISessionTitleGenerationService
{
    /// <summary>
    /// Fire-and-forget: generates an LLM title for the given session,
    /// writes it to .meta.json, and emits a SessionTitleUpdated event
    /// </summary>
    void EnqueueTitleGeneration(
        string sessionId,
        string firstMessageText,
        string transcriptPath,
        string workingDirectory,
        string locale);
}
