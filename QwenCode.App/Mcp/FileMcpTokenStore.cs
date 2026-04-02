using System.Text;
using QwenCode.App.Infrastructure;

namespace QwenCode.App.Mcp;

public sealed class FileMcpTokenStore(IDesktopEnvironmentPaths environmentPaths) : IMcpTokenStore
{
    public bool HasToken(string serverName) => File.Exists(GetTokenPath(serverName));

    public async Task<string?> GetTokenAsync(string serverName, CancellationToken cancellationToken = default)
    {
        var path = GetTokenPath(serverName);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task SaveTokenAsync(string serverName, string tokenPayload, CancellationToken cancellationToken = default)
    {
        var path = GetTokenPath(serverName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, tokenPayload, Encoding.UTF8, cancellationToken);
    }

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
