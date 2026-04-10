using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Auth;

/// <summary>
/// Represents the File Qwen O Auth Credential Store
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
public sealed class FileQwenOAuthCredentialStore(IDesktopEnvironmentPaths environmentPaths) : IQwenOAuthCredentialStore
{
    /// <summary>
    /// Gets the credential path
    /// </summary>
    public string CredentialPath => Path.Combine(environmentPaths.HomeDirectory, ".qwen", "oauth_creds.json");

    /// <summary>
    /// Executes exists
    /// </summary>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool Exists() => File.Exists(CredentialPath);

    /// <summary>
    /// Reads async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to qwen o auth credentials?</returns>
    public async Task<QwenOAuthCredentials?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(CredentialPath))
        {
            return null;
        }

        try
        {
            var root = JsonNode.Parse(await File.ReadAllTextAsync(CredentialPath, cancellationToken)) as JsonObject;
            if (root is null)
            {
                return null;
            }

            return new QwenOAuthCredentials
            {
                AccessToken = root["access_token"]?.GetValue<string?>() ?? string.Empty,
                RefreshToken = root["refresh_token"]?.GetValue<string?>() ?? string.Empty,
                IdToken = root["id_token"]?.GetValue<string?>() ?? string.Empty,
                TokenType = root["token_type"]?.GetValue<string?>() ?? "Bearer",
                ResourceUrl = root["resource_url"]?.GetValue<string?>() ?? string.Empty,
                ExpiresAtUtc = root["expiry_date"]?.GetValue<long?>() is { } expiryEpoch
                    ? DateTimeOffset.FromUnixTimeMilliseconds(expiryEpoch)
                    : null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes async
    /// </summary>
    /// <param name="credentials">The credentials</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task WriteAsync(QwenOAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CredentialPath)!);
        var root = new JsonObject
        {
            ["access_token"] = credentials.AccessToken,
            ["refresh_token"] = string.IsNullOrWhiteSpace(credentials.RefreshToken) ? null : credentials.RefreshToken,
            ["id_token"] = string.IsNullOrWhiteSpace(credentials.IdToken) ? null : credentials.IdToken,
            ["token_type"] = string.IsNullOrWhiteSpace(credentials.TokenType) ? "Bearer" : credentials.TokenType,
            ["resource_url"] = string.IsNullOrWhiteSpace(credentials.ResourceUrl) ? null : credentials.ResourceUrl,
            ["expiry_date"] = credentials.ExpiresAtUtc?.ToUnixTimeMilliseconds()
        };

        await File.WriteAllTextAsync(
            CredentialPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    /// <summary>
    /// Executes clear async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(CredentialPath))
        {
            File.Delete(CredentialPath);
        }

        return Task.CompletedTask;
    }
}
