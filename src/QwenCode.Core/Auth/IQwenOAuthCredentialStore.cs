using QwenCode.Core.Models;

namespace QwenCode.Core.Auth;

/// <summary>
/// Defines the contract for Qwen O Auth Credential Store
/// </summary>
public interface IQwenOAuthCredentialStore
{
    /// <summary>
    /// Gets the credential path
    /// </summary>
    string CredentialPath { get; }

    /// <summary>
    /// Executes exists
    /// </summary>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool Exists();

    /// <summary>
    /// Reads async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to qwen o auth credentials?</returns>
    Task<QwenOAuthCredentials?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes async
    /// </summary>
    /// <param name="credentials">The credentials</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task WriteAsync(QwenOAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes clear async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
