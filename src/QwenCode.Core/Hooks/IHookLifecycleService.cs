using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

/// <summary>
/// Defines the contract for Hook Lifecycle Service
/// </summary>
public interface IHookLifecycleService
{
    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to hook lifecycle result</returns>
    Task<HookLifecycleResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        HookInvocationRequest request,
        CancellationToken cancellationToken = default);
}
