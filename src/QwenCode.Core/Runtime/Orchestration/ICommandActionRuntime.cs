using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Defines the contract for Command Action Runtime
/// </summary>
public interface ICommandActionRuntime
{
    /// <summary>
    /// Attempts to invoke async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="prompt">The prompt content</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to command invocation result?</returns>
    Task<CommandInvocationResult?> TryInvokeAsync(
        WorkspacePaths paths,
        string prompt,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
