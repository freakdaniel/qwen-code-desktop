namespace QwenCode.App.Mcp;

public interface IMcpTokenStore
{
    bool HasToken(string serverName);

    Task<string?> GetTokenAsync(string serverName, CancellationToken cancellationToken = default);

    Task SaveTokenAsync(string serverName, string tokenPayload, CancellationToken cancellationToken = default);

    Task DeleteTokenAsync(string serverName, CancellationToken cancellationToken = default);
}
