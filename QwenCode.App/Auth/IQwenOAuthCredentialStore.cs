using QwenCode.App.Models;

namespace QwenCode.App.Auth;

public interface IQwenOAuthCredentialStore
{
    string CredentialPath { get; }

    bool Exists();

    Task<QwenOAuthCredentials?> ReadAsync(CancellationToken cancellationToken = default);

    Task WriteAsync(QwenOAuthCredentials credentials, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
