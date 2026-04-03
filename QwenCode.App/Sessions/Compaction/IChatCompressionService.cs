using QwenCode.App.Models;

namespace QwenCode.App.Sessions;

public interface IChatCompressionService
{
    Task<ChatCompressionCheckpoint?> TryCreateCheckpointAsync(
        string transcriptPath,
        CancellationToken cancellationToken = default);
}
