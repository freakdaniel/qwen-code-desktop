using QwenCode.App.Models;

namespace QwenCode.App.Prompts;

/// <summary>
/// Defines the contract for Prompt Registry Service
/// </summary>
public interface IPromptRegistryService
{
    /// <summary>
    /// Gets snapshot async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to prompt registry snapshot</returns>
    Task<PromptRegistrySnapshot> GetSnapshotAsync(
        WorkspacePaths paths,
        GetPromptRegistryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    Task<McpPromptInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        InvokePromptRegistryEntryRequest request,
        CancellationToken cancellationToken = default);
}
