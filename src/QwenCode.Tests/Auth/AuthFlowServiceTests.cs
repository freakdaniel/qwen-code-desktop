using QwenCode.Core.Auth;

namespace QwenCode.Tests.Auth;

public sealed class AuthFlowServiceTests
{
    [Fact]
    public async Task ConfigureQwenOAuthAsync_WritesCredentialsAndReportsConnectedStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-auth-oauth-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var store = new FileQwenOAuthCredentialStore(environmentPaths);
            var service = new AuthFlowService(
                runtimeProfileService,
                environmentPaths,
                store,
                new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))),
                new FakeAuthUrlLauncher());

            var snapshot = await service.ConfigureQwenOAuthAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ConfigureQwenOAuthRequest
                {
                    Scope = "user",
                    AccessToken = "oauth-access-token",
                    RefreshToken = "oauth-refresh-token",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
                });

            Assert.Equal("qwen-oauth", snapshot.SelectedType);
            Assert.Equal("connected", snapshot.Status);
            Assert.True(snapshot.HasApiKey);
            Assert.True(snapshot.HasQwenOAuthCredentials);
            Assert.True(File.Exists(store.CredentialPath));

            var settingsPath = Path.Combine(homeRoot, ".qwen", "settings.json");
            Assert.Contains("\"selectedType\": \"qwen-oauth\"", await File.ReadAllTextAsync(settingsPath));
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
    public async Task ConfigureCodingPlanAsync_WritesCodingPlanTemplateAndStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-auth-coding-plan-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var store = new FileQwenOAuthCredentialStore(environmentPaths);
            var service = new AuthFlowService(
                runtimeProfileService,
                environmentPaths,
                store,
                new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))),
                new FakeAuthUrlLauncher());

            var snapshot = await service.ConfigureCodingPlanAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ConfigureCodingPlanAuthRequest
                {
                    Scope = "project",
                    Region = "global",
                    ApiKey = "coding-plan-key",
                    Model = "qwen3-coder-next"
                });

            Assert.Equal("openai", snapshot.SelectedType);
            Assert.Equal("connected", snapshot.Status);
            Assert.Equal("qwen3-coder-next", snapshot.Model);
            Assert.Equal("BAILIAN_CODING_PLAN_API_KEY", snapshot.ApiKeyEnvironmentVariable);

            var settingsPath = Path.Combine(workspaceRoot, ".qwen", "settings.json");
            var settings = await File.ReadAllTextAsync(settingsPath);
            Assert.Contains("\"BAILIAN_CODING_PLAN_API_KEY\": \"coding-plan-key\"", settings);
            Assert.Contains("\"region\": \"global\"", settings);
            Assert.Contains("\"qwen3-coder-plus\"", settings);
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
    public async Task StartQwenOAuthDeviceFlowAsync_CompletesBrowserFlowAndPersistsCredentials()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-auth-device-flow-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            var launcher = new FakeAuthUrlLauncher();
            var pollCount = 0;
            var handler = new RecordingHttpMessageHandler(async (request, _) =>
            {
                var content = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
                if (request.RequestUri?.AbsoluteUri.Contains("/device/code", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """
                            {
                              "device_code": "device-code-123",
                              "user_code": "QWEN-123",
                              "verification_uri": "https://chat.qwen.ai/device",
                              "verification_uri_complete": "https://chat.qwen.ai/device?user_code=QWEN-123",
                              "expires_in": 900
                            }
                            """,
                            Encoding.UTF8,
                            "application/json")
                    };
                }

                pollCount++;
                if (pollCount == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(
                            """
                            {
                              "error": "authorization_pending",
                              "error_description": "Still waiting"
                            }
                            """,
                            Encoding.UTF8,
                            "application/json")
                    };
                }

                Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", content);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "access_token": "device-flow-access-token",
                          "refresh_token": "device-flow-refresh-token",
                          "token_type": "Bearer",
                          "expires_in": 3600,
                          "resource_url": "https://chat.qwen.ai"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            });

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var store = new FileQwenOAuthCredentialStore(environmentPaths);
            var service = new AuthFlowService(
                runtimeProfileService,
                environmentPaths,
                store,
                new HttpClient(handler),
                launcher);

            var snapshots = new List<AuthStatusSnapshot>();
            service.AuthChanged += (_, snapshot) => snapshots.Add(snapshot);

            var startSnapshot = await service.StartQwenOAuthDeviceFlowAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartQwenOAuthDeviceFlowRequest
                {
                    Scope = "project"
                });

            Assert.NotNull(startSnapshot.DeviceFlow);
            Assert.Equal("pending", startSnapshot.DeviceFlow!.Status);
            Assert.Equal("QWEN-123", startSnapshot.DeviceFlow.UserCode);
            Assert.Equal("https://chat.qwen.ai/device?user_code=QWEN-123", launcher.LastUrl);

            var completedSnapshot = await WaitForAsync(
                () => snapshots.LastOrDefault(snapshot => snapshot.DeviceFlow?.Status == "succeeded" && snapshot.Status == "connected"),
                TimeSpan.FromSeconds(5));

            Assert.NotNull(completedSnapshot);
            Assert.True(completedSnapshot!.HasQwenOAuthCredentials);
            Assert.Equal("connected", completedSnapshot.Status);
            Assert.Equal("succeeded", completedSnapshot.DeviceFlow?.Status);
            Assert.True(File.Exists(store.CredentialPath));

            var settingsPath = Path.Combine(workspaceRoot, ".qwen", "settings.json");
            Assert.Contains("\"selectedType\": \"qwen-oauth\"", await File.ReadAllTextAsync(settingsPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("openrouter", "OpenRouter", "OPENROUTER_API_KEY", "https://openrouter.ai/api/v1/chat/completions")]
    [InlineData("deepseek", "DeepSeek", "DEEPSEEK_API_KEY", "https://api.deepseek.com/v1/chat/completions")]
    [InlineData("modelscope", "ModelScope", "MODELSCOPE_API_KEY", "https://api.modelscope.cn/v1/chat/completions")]
    public void GetStatus_UsesProviderAliasMetadata(
        string authType,
        string displayName,
        string envKey,
        string endpoint)
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-auth-provider-alias-{authType}-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var systemRoot = Path.Combine(root, "system");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);
        Directory.CreateDirectory(systemRoot);

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                $$"""
                {
                  "security": {
                    "auth": {
                      "selectedType": "{{authType}}"
                    }
                  },
                  "model": {
                    "name": "provider-model"
                  }
                }
                """);
            Environment.SetEnvironmentVariable(envKey, "provider-key");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var store = new FileQwenOAuthCredentialStore(environmentPaths);
            var service = new AuthFlowService(
                runtimeProfileService,
                environmentPaths,
                store,
                new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))),
                new FakeAuthUrlLauncher());

            var snapshot = service.GetStatus(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.Equal(authType, snapshot.SelectedType);
            Assert.Equal(displayName, snapshot.DisplayName);
            Assert.Equal(envKey, snapshot.ApiKeyEnvironmentVariable);
            Assert.Equal(endpoint, snapshot.Endpoint);
            Assert.True(snapshot.HasApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, null);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<T?> WaitForAsync<T>(Func<T?> valueFactory, TimeSpan timeout)
        where T : class
    {
        var startedAt = DateTime.UtcNow;
        while (DateTime.UtcNow - startedAt < timeout)
        {
            var value = valueFactory();
            if (value is not null)
            {
                return value;
            }

            await Task.Delay(50);
        }

        return null;
    }
}
