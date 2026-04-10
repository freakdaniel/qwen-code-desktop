using QwenCode.Core.Models;

namespace QwenCode.Core.Ide;

/// <summary>
/// Defines the contract for Ide Client Service
/// </summary>
public interface IIdeClientService
{
    /// <summary>
    /// Gets snapshot
    /// </summary>
    /// <returns>The resulting ide connection snapshot</returns>
    IdeConnectionSnapshot GetSnapshot();

    /// <summary>
    /// Connects async
    /// </summary>
    /// <param name="workspaceRoot">The workspace root</param>
    /// <param name="processCommand">The process command</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide connection snapshot</returns>
    Task<IdeConnectionSnapshot> ConnectAsync(
        string workspaceRoot,
        string processCommand = "",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates context
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    /// <returns>The resulting ide context snapshot</returns>
    IdeContextSnapshot UpdateContext(IdeContextSnapshot snapshot);

    /// <summary>
    /// Opens diff async
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="newContent">The new content</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide diff update result</returns>
    Task<IdeDiffUpdateResult> OpenDiffAsync(
        string filePath,
        string newContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes diff async
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="suppressNotification">The suppress notification</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string?</returns>
    Task<string?> CloseDiffAsync(
        string filePath,
        bool suppressNotification = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves diff from cli async
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="outcome">The outcome</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ResolveDiffFromCliAsync(
        string filePath,
        string outcome,
        CancellationToken cancellationToken = default);
}
