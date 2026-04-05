using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

/// <summary>
/// Defines the contract for User Prompt Hook Service
/// </summary>
public interface IUserPromptHookService
{
    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to user prompt hook result</returns>
    Task<UserPromptHookResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        UserPromptHookRequest request,
        CancellationToken cancellationToken = default);
}
