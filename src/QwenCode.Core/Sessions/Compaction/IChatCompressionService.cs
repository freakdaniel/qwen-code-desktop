using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

/// <summary>
/// Defines the contract for Chat Compression Service
/// </summary>
public interface IChatCompressionService
{
    /// <summary>
    /// Attempts to create checkpoint async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="transcriptPath">The transcript path</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to chat compression checkpoint?</returns>
    Task<ChatCompressionCheckpoint?> TryCreateCheckpointAsync(
        QwenRuntimeProfile runtimeProfile,
        string transcriptPath,
        CancellationToken cancellationToken = default);
}
