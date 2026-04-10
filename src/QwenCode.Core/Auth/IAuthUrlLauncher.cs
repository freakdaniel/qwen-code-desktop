namespace QwenCode.Core.Auth;

/// <summary>
/// Defines the contract for Auth Url Launcher
/// </summary>
public interface IAuthUrlLauncher
{
    /// <summary>
    /// Executes launch async
    /// </summary>
    /// <param name="url">The url</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to bool</returns>
    Task<bool> LaunchAsync(string url, CancellationToken cancellationToken = default);
}
