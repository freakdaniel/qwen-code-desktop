using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public interface IIdeClientService
{
    IdeConnectionSnapshot GetSnapshot();

    Task<IdeConnectionSnapshot> ConnectAsync(
        string workspaceRoot,
        string processCommand = "",
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    IdeContextSnapshot UpdateContext(IdeContextSnapshot snapshot);

    Task<IdeDiffUpdateResult> OpenDiffAsync(
        string filePath,
        string newContent,
        CancellationToken cancellationToken = default);

    Task<string?> CloseDiffAsync(
        string filePath,
        bool suppressNotification = false,
        CancellationToken cancellationToken = default);

    Task ResolveDiffFromCliAsync(
        string filePath,
        string outcome,
        CancellationToken cancellationToken = default);
}
