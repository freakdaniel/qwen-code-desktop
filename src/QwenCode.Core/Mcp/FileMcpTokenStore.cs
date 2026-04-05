using System.Text;
using QwenCode.App.Infrastructure;

namespace QwenCode.App.Mcp;

/// <summary>
/// Represents the File Mcp Token Store
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
public sealed class FileMcpTokenStore(IDesktopEnvironmentPaths environmentPaths) : IMcpTokenStore
{
    /// <summary>
    /// Executes has token
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool HasToken(string serverName) => File.Exists(GetTokenPath(serverName));

    /// <summary>
    /// Gets token async
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string?</returns>
    public async Task<string?> GetTokenAsync(string serverName, CancellationToken cancellationToken = default)
    {
        var path = GetTokenPath(serverName);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <summary>
    /// Saves token async
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <param name="tokenPayload">The token payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task SaveTokenAsync(string serverName, string tokenPayload, CancellationToken cancellationToken = default)
    {
        var path = GetTokenPath(serverName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, tokenPayload, Encoding.UTF8, cancellationToken);
    }

    /// <summary>
    /// Deletes token async
    /// </summary>
    /// <param name="serverName">The server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task DeleteTokenAsync(string serverName, CancellationToken cancellationToken = default)
    {
        var path = GetTokenPath(serverName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetTokenPath(string serverName) =>
        Path.Combine(environmentPaths.HomeDirectory, ".qwen", "mcp", "tokens", $"{Sanitize(serverName)}.json");

    private static string Sanitize(string value) =>
        new(value.Select(static character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
}
