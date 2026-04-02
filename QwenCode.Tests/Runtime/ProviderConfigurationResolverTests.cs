using QwenCode.App.Auth;

namespace QwenCode.Tests.Runtime;

public sealed class ProviderConfigurationResolverTests
{
    [Fact]
    public void ProviderConfigurationResolver_Resolve_ReadsMergedSettingsAndModelProviderOverrides()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "openai",
                      "baseUrl": "https://should-not-win.example/v1",
                      "apiKey": "direct-settings-key"
                    }
                  },
                  "env": {
                    "CUSTOM_OPENAI_KEY": "env-settings-key",
                    "OPENAI_BASE_URL": "https://env-base.example/v1"
                  },
                  "model": {
                    "name": "qwen-max",
                    "generationConfig": {
                      "customHeaders": {
                        "X-Settings-Header": "settings"
                      },
                      "extra_body": {
                        "reasoning_mode": "balanced"
                      }
                    }
                  },
                  "modelProviders": {
                    "openai": [
                      {
                        "id": "qwen-max",
                        "envKey": "CUSTOM_OPENAI_KEY",
                        "baseUrl": "https://provider.example/v1",
                        "generationConfig": {
                          "customHeaders": {
                            "X-Provider-Header": "provider"
                          },
                          "extra_body": {
                            "thinking": {
                              "type": "enabled"
                            }
                          }
                        }
                      }
                    ]
                  }
                }
                """);

            var runtimeProfile = new QwenRuntimeProfile
            {
                ProjectRoot = workspaceRoot,
                GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeBaseDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeSource = "project-settings",
                ProjectDataDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test"),
                ChatsDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test", "chats"),
                HistoryDirectory = Path.Combine(homeRoot, ".qwen", "history", "test"),
                ContextFileNames = ["QWEN.md"],
                ContextFilePaths = [Path.Combine(workspaceRoot, "QWEN.md")],
                ApprovalProfile = new ApprovalProfile
                {
                    DefaultMode = "default",
                    ConfirmShellCommands = true,
                    ConfirmFileEdits = true,
                    AllowRules = [],
                    AskRules = [],
                    DenyRules = []
                }
            };

            var resolver = new ProviderConfigurationResolver(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var resolved = resolver.Resolve(
                new AssistantTurnRequest
                {
                    SessionId = "resolver-session",
                    Prompt = "Resolve runtime settings.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "resolver-session.jsonl"),
                    RuntimeProfile = runtimeProfile,
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                },
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    Model = string.Empty,
                    Endpoint = string.Empty,
                    ApiKey = string.Empty,
                    ApiKeyEnvironmentVariable = "QWENCODE_ASSISTANT_API_KEY"
                });

            Assert.Equal("openai", resolved.AuthType);
            Assert.Equal("qwen-max", resolved.Model);
            Assert.Equal("https://provider.example/v1/chat/completions", resolved.Endpoint);
            Assert.Equal("env-settings-key", resolved.ApiKey);
            Assert.Equal("CUSTOM_OPENAI_KEY", resolved.ApiKeyEnvironmentVariable);
            Assert.Equal("settings", resolved.Headers["X-Settings-Header"]);
            Assert.Equal("provider", resolved.Headers["X-Provider-Header"]);
            Assert.Equal("balanced", resolved.ExtraBody["reasoning_mode"]?.GetValue<string>());
            Assert.Equal("enabled", resolved.ExtraBody["thinking"]?["type"]?.GetValue<string>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderConfigurationResolver_Resolve_UsesPersistedQwenOAuthCredentials()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-oauth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            await File.WriteAllTextAsync(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "qwen-oauth"
                    }
                  },
                  "model": {
                    "name": "qwen3-coder-plus"
                  }
                }
                """);

            var store = new FileQwenOAuthCredentialStore(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            await store.WriteAsync(
                new QwenOAuthCredentials
                {
                    AccessToken = "persisted-oauth-token",
                    RefreshToken = "refresh-token",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
                });

            var tokenManager = new QwenOAuthTokenManager(
                store,
                new FakeDesktopEnvironmentPaths(homeRoot, systemRoot),
                new HttpClient(new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))));

            var runtimeProfile = new QwenRuntimeProfile
            {
                ProjectRoot = workspaceRoot,
                GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeBaseDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeSource = "project-settings",
                ProjectDataDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test"),
                ChatsDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test", "chats"),
                HistoryDirectory = Path.Combine(homeRoot, ".qwen", "history", "test"),
                ContextFileNames = ["QWEN.md"],
                ContextFilePaths = [Path.Combine(workspaceRoot, "QWEN.md")],
                ApprovalProfile = new ApprovalProfile
                {
                    DefaultMode = "default",
                    ConfirmShellCommands = true,
                    ConfirmFileEdits = true,
                    AllowRules = [],
                    AskRules = [],
                    DenyRules = []
                }
            };

            var resolver = new ProviderConfigurationResolver(
                new FakeDesktopEnvironmentPaths(homeRoot, systemRoot),
                store,
                tokenManager);
            var resolved = resolver.Resolve(
                new AssistantTurnRequest
                {
                    SessionId = "oauth-session",
                    Prompt = "Resolve qwen oauth runtime settings.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "oauth-session.jsonl"),
                    RuntimeProfile = runtimeProfile,
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                },
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    Model = string.Empty,
                    Endpoint = string.Empty,
                    ApiKey = string.Empty,
                    ApiKeyEnvironmentVariable = string.Empty
                });

            Assert.Equal("qwen-oauth", resolved.AuthType);
            Assert.Equal("persisted-oauth-token", resolved.ApiKey);
            Assert.Equal("QWEN_OAUTH_ACCESS_TOKEN", resolved.ApiKeyEnvironmentVariable);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderConfigurationResolver_Resolve_RefreshesExpiredQwenOAuthCredentials()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-provider-oauth-refresh-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            await File.WriteAllTextAsync(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "auth": {
                      "selectedType": "qwen-oauth"
                    }
                  },
                  "model": {
                    "name": "qwen3-coder-plus"
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var store = new FileQwenOAuthCredentialStore(environmentPaths);
            await store.WriteAsync(
                new QwenOAuthCredentials
                {
                    AccessToken = "expired-oauth-token",
                    RefreshToken = "refresh-token",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
                });

            var tokenManager = new QwenOAuthTokenManager(
                store,
                environmentPaths,
                new HttpClient(
                    new RecordingHttpMessageHandler((_, _) =>
                        Task.FromResult(
                            new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = new StringContent(
                                    """
                                    {
                                      "access_token": "fresh-oauth-token",
                                      "refresh_token": "fresh-refresh-token",
                                      "token_type": "Bearer",
                                      "expires_in": 3600
                                    }
                                    """,
                                    Encoding.UTF8,
                                    "application/json")
                            }))));

            var runtimeProfile = new QwenRuntimeProfile
            {
                ProjectRoot = workspaceRoot,
                GlobalQwenDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeBaseDirectory = Path.Combine(homeRoot, ".qwen"),
                RuntimeSource = "project-settings",
                ProjectDataDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test"),
                ChatsDirectory = Path.Combine(homeRoot, ".qwen", "projects", "test", "chats"),
                HistoryDirectory = Path.Combine(homeRoot, ".qwen", "history", "test"),
                ContextFileNames = ["QWEN.md"],
                ContextFilePaths = [Path.Combine(workspaceRoot, "QWEN.md")],
                ApprovalProfile = new ApprovalProfile
                {
                    DefaultMode = "default",
                    ConfirmShellCommands = true,
                    ConfirmFileEdits = true,
                    AllowRules = [],
                    AskRules = [],
                    DenyRules = []
                }
            };

            var resolver = new ProviderConfigurationResolver(environmentPaths, store, tokenManager);
            var resolved = resolver.Resolve(
                new AssistantTurnRequest
                {
                    SessionId = "oauth-refresh-session",
                    Prompt = "Resolve refreshed qwen oauth runtime settings.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "oauth-refresh-session.jsonl"),
                    RuntimeProfile = runtimeProfile,
                    GitBranch = "main",
                    ToolExecution = new NativeToolExecutionResult
                    {
                        ToolName = string.Empty,
                        Status = "not-requested",
                        ApprovalState = "allow",
                        WorkingDirectory = workspaceRoot,
                        Output = string.Empty,
                        ErrorMessage = string.Empty,
                        ExitCode = 0,
                        ChangedFiles = []
                    }
                },
                new NativeAssistantRuntimeOptions
                {
                    Provider = "qwen-compatible",
                    Model = string.Empty,
                    Endpoint = string.Empty,
                    ApiKey = string.Empty,
                    ApiKeyEnvironmentVariable = string.Empty
                });

            Assert.Equal("qwen-oauth", resolved.AuthType);
            Assert.Equal("fresh-oauth-token", resolved.ApiKey);

            var persisted = await store.ReadAsync();
            Assert.NotNull(persisted);
            Assert.Equal("fresh-oauth-token", persisted!.AccessToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
