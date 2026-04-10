namespace QwenCode.Core.Mcp;

/// <summary>
/// Defines the contract for Mcp Token Store
/// </summary>
public interface IMcpTokenStore
{
    /// <summary>
    /// Executes has token
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    bool HasToken(string serverName);

    /// <summary>
    /// Gets token async
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string?</returns>
    Task<string?> GetTokenAsync(string serverName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves token async
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <param name="tokenPayload">The token payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SaveTokenAsync(string serverName, string tokenPayload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes token async
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeleteTokenAsync(string serverName, CancellationToken cancellationToken = default);
}
