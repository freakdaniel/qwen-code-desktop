using System.Net;
using System.Net.Http.Headers;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Auth;

/// <summary>
/// Represents the Qwen O Auth Token Manager
/// </summary>
/// <param name="credentialStore">The credential store</param>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="httpClient">The http client</param>
public sealed class QwenOAuthTokenManager(
    IQwenOAuthCredentialStore credentialStore,
    IDesktopEnvironmentPaths environmentPaths,
    HttpClient httpClient) : IQwenOAuthTokenManager
{
    private const string QwenOAuthBaseUrl = "https://chat.qwen.ai";
    private const string QwenOAuthTokenEndpoint = $"{QwenOAuthBaseUrl}/api/v1/oauth2/token";
    private const string QwenOAuthClientId = "f0304373b74a44d2b584a3fb70ca9e56";
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FileCheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan InitialLockRetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private QwenOAuthCredentials? cachedCredentials;
    private DateTimeOffset? cachedFileWriteAtUtc;
    private DateTimeOffset lastFileCheckAtUtc = DateTimeOffset.MinValue;
    private Task<QwenOAuthCredentials?>? refreshTask;

    /// <summary>
    /// Gets or sets the last error
    /// </summary>
    public string LastError { get; private set; } = string.Empty;

    /// <summary>
    /// Gets current credentials async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to qwen o auth credentials?</returns>
    public async Task<QwenOAuthCredentials?> GetCurrentCredentialsAsync(CancellationToken cancellationToken = default)
    {
        await ReloadFromFileIfNeededAsync(force: false, cancellationToken);
        return cachedCredentials;
    }

    /// <summary>
    /// Gets valid credentials async
    /// </summary>
    /// <param name="forceRefresh">The force refresh</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to qwen o auth credentials?</returns>
    public async Task<QwenOAuthCredentials?> GetValidCredentialsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        await ReloadFromFileIfNeededAsync(force: false, cancellationToken);
        if (!forceRefresh && IsTokenValid(cachedCredentials))
        {
            return cachedCredentials;
        }

        Task<QwenOAuthCredentials?> currentRefreshTask;
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            refreshTask ??= RefreshCredentialsCoreAsync(forceRefresh);
            currentRefreshTask = refreshTask;
        }
        finally
        {
            stateGate.Release();
        }

        try
        {
            return await currentRefreshTask.WaitAsync(cancellationToken);
        }
        finally
        {
            await stateGate.WaitAsync(cancellationToken);
            try
            {
                if (ReferenceEquals(refreshTask, currentRefreshTask))
                {
                    refreshTask = null;
                }
            }
            finally
            {
                stateGate.Release();
            }
        }
    }

    /// <summary>
    /// Executes store credentials async
    /// </summary>
    /// <param name="credentials">The credentials</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task StoreCredentialsAsync(QwenOAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        await credentialStore.WriteAsync(credentials, cancellationToken);
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            cachedCredentials = credentials;
            cachedFileWriteAtUtc = File.Exists(credentialStore.CredentialPath)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(credentialStore.CredentialPath), TimeSpan.Zero)
                : DateTimeOffset.UtcNow;
            lastFileCheckAtUtc = DateTimeOffset.UtcNow;
            LastError = string.Empty;
        }
        finally
        {
            stateGate.Release();
        }
    }

    /// <summary>
    /// Executes clear credentials async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task ClearCredentialsAsync(CancellationToken cancellationToken = default)
    {
        await credentialStore.ClearAsync(cancellationToken);
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            cachedCredentials = null;
            cachedFileWriteAtUtc = null;
            lastFileCheckAtUtc = DateTimeOffset.UtcNow;
            refreshTask = null;
            LastError = string.Empty;
        }
        finally
        {
            stateGate.Release();
        }
    }

    private async Task ReloadFromFileIfNeededAsync(bool force, CancellationToken cancellationToken)
    {
        var shouldCheck = force ||
                          DateTimeOffset.UtcNow - lastFileCheckAtUtc >= FileCheckInterval;
        if (!shouldCheck)
        {
            return;
        }

        await stateGate.WaitAsync(cancellationToken);
        try
        {
            shouldCheck = force ||
                          DateTimeOffset.UtcNow - lastFileCheckAtUtc >= FileCheckInterval;
            if (!shouldCheck)
            {
                return;
            }

            if (!credentialStore.Exists())
            {
                cachedCredentials = null;
                cachedFileWriteAtUtc = null;
                lastFileCheckAtUtc = DateTimeOffset.UtcNow;
                return;
            }

            var fileWriteAtUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(credentialStore.CredentialPath), TimeSpan.Zero);
            if (!force &&
                cachedCredentials is not null &&
                cachedFileWriteAtUtc.HasValue &&
                cachedFileWriteAtUtc.Value >= fileWriteAtUtc)
            {
                lastFileCheckAtUtc = DateTimeOffset.UtcNow;
                return;
            }

            cachedCredentials = await credentialStore.ReadAsync(cancellationToken);
            cachedFileWriteAtUtc = fileWriteAtUtc;
            lastFileCheckAtUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            stateGate.Release();
        }
    }

    private async Task<QwenOAuthCredentials?> RefreshCredentialsCoreAsync(bool forceRefresh)
    {
        var lockPath = Path.Combine(environmentPaths.HomeDirectory, ".qwen", "oauth_creds.lock");
        await AcquireLockAsync(lockPath);
        try
        {
            await ReloadFromFileIfNeededAsync(force: true, CancellationToken.None);
            if (!forceRefresh && IsTokenValid(cachedCredentials))
            {
                return cachedCredentials;
            }

            if (cachedCredentials is null || string.IsNullOrWhiteSpace(cachedCredentials.RefreshToken))
            {
                return cachedCredentials is not null && IsTokenValid(cachedCredentials) ? cachedCredentials : null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, QwenOAuthTokenEndpoint)
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", cachedCredentials.RefreshToken),
                    new KeyValuePair<string, string>("client_id", QwenOAuthClientId)
                ])
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, CancellationToken.None);
            var content = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await ClearCredentialsAsync(CancellationToken.None);
                LastError = "Refresh token expired or invalid. Please authenticate again.";
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Qwen OAuth refresh failed with status {(int)response.StatusCode}.";
                return null;
            }

            var refreshedCredentials = ParseRefreshResponse(content, cachedCredentials);
            await StoreCredentialsAsync(refreshedCredentials, CancellationToken.None);
            return refreshedCredentials;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            return null;
        }
        finally
        {
            ReleaseLock(lockPath);
        }
    }

    private static QwenOAuthCredentials ParseRefreshResponse(string content, QwenOAuthCredentials currentCredentials)
    {
        using var document = System.Text.Json.JsonDocument.Parse(content);
        var root = document.RootElement;
        var expiresIn = root.TryGetProperty("expires_in", out var expiresInProperty) &&
                        expiresInProperty.ValueKind == System.Text.Json.JsonValueKind.Number
            ? expiresInProperty.GetInt32()
            : 0;

        return new QwenOAuthCredentials
        {
            AccessToken = root.GetProperty("access_token").GetString() ?? string.Empty,
            RefreshToken = root.TryGetProperty("refresh_token", out var refreshTokenProperty)
                ? refreshTokenProperty.GetString() ?? currentCredentials.RefreshToken
                : currentCredentials.RefreshToken,
            TokenType = root.TryGetProperty("token_type", out var tokenTypeProperty)
                ? tokenTypeProperty.GetString() ?? currentCredentials.TokenType
                : currentCredentials.TokenType,
            ResourceUrl = root.TryGetProperty("resource_url", out var resourceUrlProperty)
                ? resourceUrlProperty.GetString() ?? currentCredentials.ResourceUrl
                : currentCredentials.ResourceUrl,
            ExpiresAtUtc = expiresIn > 0
                ? DateTimeOffset.UtcNow.AddSeconds(expiresIn)
                : currentCredentials.ExpiresAtUtc
        };
    }

    private static bool IsTokenValid(QwenOAuthCredentials? credentials) =>
        credentials is not null &&
        !string.IsNullOrWhiteSpace(credentials.AccessToken) &&
        (!credentials.ExpiresAtUtc.HasValue || credentials.ExpiresAtUtc.Value - RefreshBuffer > DateTimeOffset.UtcNow);

    private static async Task AcquireLockAsync(string lockPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var startedAt = DateTimeOffset.UtcNow;
        var retryDelay = InitialLockRetryDelay;

        while (DateTimeOffset.UtcNow - startedAt < LockTimeout)
        {
            try
            {
                await using var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(Environment.ProcessId.ToString());
                await writer.FlushAsync();
                return;
            }
            catch (IOException)
            {
                if (File.Exists(lockPath) &&
                    DateTime.UtcNow - File.GetLastWriteTimeUtc(lockPath) > LockTimeout)
                {
                    try
                    {
                        File.Delete(lockPath);
                        continue;
                    }
                    catch
                    {
                    }
                }

                await Task.Delay(retryDelay);
                var nextDelayMs = Math.Min(retryDelay.TotalMilliseconds * 1.5, 2000);
                retryDelay = TimeSpan.FromMilliseconds(nextDelayMs);
            }
        }

        throw new TimeoutException("Timed out waiting for the Qwen OAuth credential lock.");
    }

    private static void ReleaseLock(string lockPath)
    {
        try
        {
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
            }
        }
        catch
        {
        }
    }
}
