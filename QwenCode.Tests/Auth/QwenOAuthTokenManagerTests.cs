using System.Net;
using System.Text;
using QwenCode.App.Auth;

namespace QwenCode.Tests.Auth;

public sealed class QwenOAuthTokenManagerTests
{
    [Fact]
    public async Task GetValidCredentialsAsync_RefreshesExpiredCredentialsAndPersistsUpdatedTokens()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-token-refresh-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot);
            var store = new FileQwenOAuthCredentialStore(environmentPaths);
            await store.WriteAsync(
                new QwenOAuthCredentials
                {
                    AccessToken = "expired-access-token",
                    RefreshToken = "refresh-token-123",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
                });

            var refreshRequests = 0;
            var handler = new RecordingHttpMessageHandler(async (request, _) =>
            {
                refreshRequests++;
                var content = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
                Assert.Contains("grant_type=refresh_token", content);
                Assert.Contains("refresh_token=refresh-token-123", content);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "access_token": "fresh-access-token",
                          "refresh_token": "fresh-refresh-token",
                          "token_type": "Bearer",
                          "expires_in": 3600,
                          "resource_url": "https://chat.qwen.ai"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            });

            var manager = new QwenOAuthTokenManager(store, environmentPaths, new HttpClient(handler));
            var refreshed = await manager.GetValidCredentialsAsync();

            Assert.NotNull(refreshed);
            Assert.Equal("fresh-access-token", refreshed!.AccessToken);
            Assert.Equal("fresh-refresh-token", refreshed.RefreshToken);
            Assert.Equal(string.Empty, manager.LastError);
            Assert.Equal(1, refreshRequests);

            var persisted = await store.ReadAsync();
            Assert.NotNull(persisted);
            Assert.Equal("fresh-access-token", persisted!.AccessToken);
            Assert.Equal("fresh-refresh-token", persisted.RefreshToken);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetValidCredentialsAsync_OnBadRequest_ClearsPersistedCredentials()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-token-clear-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot);
            var store = new FileQwenOAuthCredentialStore(environmentPaths);
            await store.WriteAsync(
                new QwenOAuthCredentials
                {
                    AccessToken = "expired-access-token",
                    RefreshToken = "stale-refresh-token",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
                });

            var handler = new RecordingHttpMessageHandler((_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(
                            """
                            {
                              "error": "invalid_grant",
                              "error_description": "Refresh token expired"
                            }
                            """,
                            Encoding.UTF8,
                            "application/json")
                    }));

            var manager = new QwenOAuthTokenManager(store, environmentPaths, new HttpClient(handler));
            var refreshed = await manager.GetValidCredentialsAsync();

            Assert.Null(refreshed);
            Assert.False(store.Exists());
            Assert.Equal("Refresh token expired or invalid. Please authenticate again.", manager.LastError);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
