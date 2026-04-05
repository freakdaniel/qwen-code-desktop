using QwenCode.App.Models;

namespace QwenCode.App.Tools;

/// <summary>
/// Defines the contract for Shell Execution Service
/// </summary>
public interface IShellExecutionService
{
    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to shell command execution result</returns>
    Task<ShellCommandExecutionResult> ExecuteAsync(
        ShellCommandRequest request,
        CancellationToken cancellationToken = default);
}
