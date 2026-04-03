namespace QwenCode.Tests.Hooks;

public sealed class UserPromptHookServiceTests
{
    [Fact]
    public async Task UserPromptHookService_ExecuteAsync_AppliesModifiedPromptAndAdditionalContext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-hooks-modify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            var scriptPath = Path.Combine(root, "modify-hook.ps1");
            File.WriteAllText(
                scriptPath,
                """
                $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
                $result = @{
                  decision = 'allow'
                  systemMessage = 'Hook system note'
                  hookSpecificOutput = @{
                    modifiedPrompt = "$($payload.prompt) [hooked]"
                    additionalContext = 'Use qwen hook context'
                  }
                }
                [Console]::Out.Write(($result | ConvertTo-Json -Compress -Depth 6))
                """);

            var command = $"& '{scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal)}'";
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                $$"""
                {
                  "hooksConfig": {
                    "enabled": true
                  },
                  "hooks": {
                    "UserPromptSubmit": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "name": "modify-prompt",
                            "command": "{{command}}"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfile = new QwenRuntimeProfileService(environmentPaths)
                .Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var service = new UserPromptHookService(
                new HookRegistryService(environmentPaths),
                new HookCommandRunner(),
                new HookOutputAggregator());

            var result = await service.ExecuteAsync(
                runtimeProfile,
                new UserPromptHookRequest
                {
                    SessionId = "session-1",
                    Prompt = "Build the missing runtime layer.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(workspaceRoot, ".qwen", "chats", "session-1.jsonl")
                });

            Assert.False(result.IsBlocked);
            Assert.Equal("Build the missing runtime layer. [hooked]", result.EffectivePrompt);
            Assert.Equal("Use qwen hook context", result.AdditionalContext);
            Assert.Equal("Hook system note", result.SystemMessage);
            Assert.Single(result.Executions);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UserPromptHookService_ExecuteAsync_IgnoresProjectHooksInUntrustedWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-hooks-untrusted-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            var scriptPath = Path.Combine(root, "block-hook.ps1");
            File.WriteAllText(
                scriptPath,
                """
                [Console]::Error.Write('Blocked by project hook')
                exit 2
                """);

            var command = $"& '{scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal)}'";
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """
                {
                  "security": {
                    "folderTrust": {
                      "enabled": true
                    }
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "trustedFolders.json"),
                BuildTrustedFoldersJson(workspaceRoot, "DO_NOT_TRUST"));
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                $$"""
                {
                  "hooksConfig": {
                    "enabled": true
                  },
                  "hooks": {
                    "UserPromptSubmit": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "name": "project-block",
                            "command": "{{command}}"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfile = new QwenRuntimeProfileService(environmentPaths)
                .Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var service = new UserPromptHookService(
                new HookRegistryService(environmentPaths),
                new HookCommandRunner(),
                new HookOutputAggregator());

            var result = await service.ExecuteAsync(
                runtimeProfile,
                new UserPromptHookRequest
                {
                    SessionId = "session-2",
                    Prompt = "Keep project hooks out of untrusted workspaces.",
                    WorkingDirectory = workspaceRoot,
                    TranscriptPath = Path.Combine(workspaceRoot, ".qwen", "chats", "session-2.jsonl")
                });

            Assert.True(runtimeProfile.FolderTrustEnabled);
            Assert.False(runtimeProfile.IsWorkspaceTrusted);
            Assert.False(result.IsBlocked);
            Assert.Equal("Keep project hooks out of untrusted workspaces.", result.EffectivePrompt);
            Assert.Empty(result.Executions);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string BuildTrustedFoldersJson(string workspaceRoot, string trustValue) =>
        $$"""
        {
          "{{workspaceRoot.Replace("\\", "\\\\", StringComparison.Ordinal)}}": "{{trustValue}}"
        }
        """;
}
