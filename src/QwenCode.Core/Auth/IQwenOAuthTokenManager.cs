using QwenCode.App.Models;

namespace QwenCode.App.Auth;

/// <summary>
/// Defines the contract for Qwen O Auth Token Manager
/// </summary>
public interface IQwenOAuthTokenManager
{
    /// <summary>
    /// Gets the last error
    /// </summary>
    string LastError { get; }

    /// <summary>
    /// Gets current credentials async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to qwen o auth credentials?</returns>
    Task<QwenOAuthCredentials?> GetCurrentCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets valid credentials async
    /// </summary>
    /// <param name="forceRefresh">The force refresh</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to qwen o auth credentials?</returns>
    Task<QwenOAuthCredentials?> GetValidCredentialsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes store credentials async
    /// </summary>
    /// <param name="credentials">The credentials</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task StoreCredentialsAsync(QwenOAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes clear credentials async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ClearCredentialsAsync(CancellationToken cancellationToken = default);
}
