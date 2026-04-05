using QwenCode.App.Models;

namespace QwenCode.App.Auth;

public interface IQwenOAuthTokenManager
{
    string LastError { get; }

    Task<QwenOAuthCredentials?> GetCurrentCredentialsAsync(CancellationToken cancellationToken = default);

    Task<QwenOAuthCredentials?> GetValidCredentialsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    Task StoreCredentialsAsync(QwenOAuthCredentials credentials, CancellationToken cancellationToken = default);

    Task ClearCredentialsAsync(CancellationToken cancellationToken = default);
}
