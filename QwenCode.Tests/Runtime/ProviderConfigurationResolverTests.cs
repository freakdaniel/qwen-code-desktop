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


}
