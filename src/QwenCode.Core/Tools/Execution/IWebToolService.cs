using QwenCode.Core.Models;

namespace QwenCode.Core.Tools;

/// <summary>
/// Defines the contract for Web Tool Service
/// </summary>
public interface IWebToolService
{
    /// <summary>
    /// Executes fetch async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    Task<string> FetchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes search async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string</returns>
    Task<string> SearchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default);
}
