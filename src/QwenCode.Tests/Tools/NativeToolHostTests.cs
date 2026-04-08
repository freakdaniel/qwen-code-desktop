namespace QwenCode.Tests.Tools;

public sealed class NativeToolHostTests
{
    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_ReadsAndWritesWithApprovalGate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-native-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "default",
                    "allow": ["Read"],
                    "ask": ["WriteFile"]
                  }
                }
                """);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), new InMemoryCronScheduler());

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            File.WriteAllText(targetFile, "alpha\nbeta\ngamma");

            var readResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "read_file",
                ArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","offset":1,"limit":1}"""
            });

            Assert.Equal("completed", readResult.Status);
            Assert.Contains("beta", readResult.Output);

            var gatedWrite = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "write_file",
                ArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"replaced"}"""
            });

            Assert.Equal("approval-required", gatedWrite.Status);

            var approvedWrite = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "write_file",
                ApproveExecution = true,
                ArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"replaced"}"""
            });

            Assert.Equal("completed", approvedWrite.Status);
            Assert.Equal("replaced", File.ReadAllText(targetFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_AppliesSpecifierScopedRules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-native-scoped-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var docsRoot = Path.Combine(workspaceRoot, "docs");
            var srcRoot = Path.Combine(workspaceRoot, "src");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "plan",
                    "allow": ["Read(./docs/**)", "Bash(git *)"],
                    "ask": ["Edit(/src/**)"],
                    "deny": ["Read(.env)"]
                  }
                }
                """);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), new InMemoryCronScheduler());

            var docsFile = Path.Combine(docsRoot, "guide.md");
            var srcFile = Path.Combine(srcRoot, "Program.cs");
            var envFile = Path.Combine(workspaceRoot, ".env");
            File.WriteAllText(docsFile, "docs-content");
            File.WriteAllText(srcFile, "class Program {}");
            File.WriteAllText(envFile, "SECRET=1");

            var allowedRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "read_file",
                ArgumentsJson = $$"""{"file_path":"{{docsFile.Replace("\\", "\\\\")}}"}"""
            });

            var deniedRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "read_file",
                ArgumentsJson = $$"""{"file_path":"{{envFile.Replace("\\", "\\\\")}}"}"""
            });

            var gatedEdit = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "write_file",
                ArgumentsJson = $$"""{"file_path":"{{srcFile.Replace("\\", "\\\\")}}","content":"updated"}"""
            });

            var allowedShell = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"git --version"}""",
                ApproveExecution = false
            });

            Assert.Equal("completed", allowedRead.Status);
            Assert.Contains("docs-content", allowedRead.Output);
            Assert.Equal("blocked", deniedRead.Status);
            Assert.Contains("Read(.env)", deniedRead.ErrorMessage);
            Assert.Equal("approval-required", gatedEdit.Status);
            Assert.Contains("Edit(/src/**)", gatedEdit.ErrorMessage);
            Assert.Equal("completed", allowedShell.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_AppliesVirtualShellPermissionSemantics()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var docsRoot = Path.Combine(workspaceRoot, "docs");
            var srcRoot = Path.Combine(workspaceRoot, "src");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var docsFile = Path.Combine(docsRoot, "guide.txt");
            var srcFile = Path.Combine(srcRoot, "output.txt");
            var envFile = Path.Combine(workspaceRoot, ".env");
            File.WriteAllText(docsFile, "docs through shell");
            File.WriteAllText(envFile, "SECRET=1");

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "plan",
                    "allow": ["Read(./docs/**)"],
                    "ask": ["Edit(/src/**)"],
                    "deny": ["Read(.env)"]
                  }
                }
                """);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), new InMemoryCronScheduler());

            var allowedShellRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = JsonSerializer.Serialize(new
                {
                    command = CrossPlatformTestSupport.GetReadFileShellCommand(
                        OperatingSystem.IsWindows() ? @"docs\guide.txt" : "docs/guide.txt")
                })
            });

            var gatedShellWrite = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = JsonSerializer.Serialize(new
                {
                    command = CrossPlatformTestSupport.GetWriteFileShellCommand(
                        OperatingSystem.IsWindows() ? @"src\output.txt" : "src/output.txt",
                        "hello")
                })
            });

            var deniedShellRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = JsonSerializer.Serialize(new
                {
                    command = CrossPlatformTestSupport.GetReadFileShellCommand(".env")
                })
            });

            Assert.Equal("completed", allowedShellRead.Status);
            Assert.Contains("docs through shell", allowedShellRead.Output);
            Assert.Equal("approval-required", gatedShellWrite.Status);
            Assert.Contains("Edit(/src/**)", gatedShellWrite.ErrorMessage);
            Assert.Equal("blocked", deniedShellRead.Status);
            Assert.Contains("Read(.env)", deniedShellRead.ErrorMessage);
            Assert.False(File.Exists(srcFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_MapsTimedOutShellCommandIntoTimeoutStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-timeout-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                new InMemoryCronScheduler(),
                shellExecutionService: new TimeoutShellExecutionService());

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ApproveExecution = true,
                ArgumentsJson = """{"command":"sleep forever"}"""
            });

            Assert.Equal("timeout", result.Status);
            Assert.Equal(-1, result.ExitCode);
            Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_WebFetch_MapsSslFailuresToFriendlyMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-webfetch-ssl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                new InMemoryCronScheduler(),
                webToolService: new ThrowingWebToolService(
                    new Exception(
                        "The SSL connection could not be established, see inner exception.",
                        new System.Security.Authentication.AuthenticationException("TLS handshake failed."))));

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "web_fetch",
                ArgumentsJson = """{"url":"https://example.com"}"""
            });

            Assert.Equal("error", result.Status);
            Assert.Contains("secure HTTPS connection", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("see inner exception", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_SavesMemoryToProjectAndGlobalScopes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-memory-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), new InMemoryCronScheduler());

            var projectResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "save_memory",
                ApproveExecution = true,
                ArgumentsJson = """{"fact":"Remember that this project uses a desktop-native qwen runtime","scope":"project"}"""
            });

            var globalResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "save_memory",
                ApproveExecution = true,
                ArgumentsJson = """{"fact":"Remember that Daniel prefers the C# port to stay monolithic","scope":"global"}"""
            });

            var projectMemoryPath = Path.Combine(workspaceRoot, "QWEN.md");
            var globalMemoryPath = Path.Combine(homeRoot, ".qwen", "QWEN.md");

            Assert.Equal("completed", projectResult.Status);
            Assert.Equal("completed", globalResult.Status);
            Assert.Contains("## Qwen Added Memories", File.ReadAllText(projectMemoryPath));
            Assert.Contains("desktop-native qwen runtime", File.ReadAllText(projectMemoryPath));
            Assert.Contains("stay monolithic", File.ReadAllText(globalMemoryPath));
            Assert.Contains(projectMemoryPath, projectResult.ChangedFiles);
            Assert.Contains(globalMemoryPath, globalResult.ChangedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_WritesTodoListIntoRuntimeStore()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-todo-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), new InMemoryCronScheduler());
            var runtimeProfile = runtimeProfileService.Inspect(sourcePaths);

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "todo_write",
                ApproveExecution = true,
                ArgumentsJson =
                    """
                    {
                      "session_id":"session-123",
                      "todos":[
                        {"id":"todo-1","content":"Port qwen core scheduling","status":"in_progress"},
                        {"id":"todo-2","content":"Add parity tests for new tools","status":"pending"},
                        {"id":"todo-3","content":"Report missing subsystems","status":"completed"}
                      ]
                    }
                    """
            });

            var todoFilePath = Path.Combine(runtimeProfile.RuntimeBaseDirectory, "todos", "session-123.json");
            var todoFileContent = File.ReadAllText(todoFilePath);

            Assert.Equal("completed", result.Status);
            Assert.Contains("Saved 3 todo item(s)", result.Output);
            Assert.Contains("todo-1", todoFileContent);
            Assert.Contains("in_progress", todoFileContent);
            Assert.Contains(todoFilePath, result.ChangedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_CompletesExitPlanModeControlTool()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-plan-exit-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), new InMemoryCronScheduler());

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "exit_plan_mode",
                ArgumentsJson = "{}"
            });

            Assert.Equal("completed", result.Status);
            Assert.Equal("exit_plan_mode", result.ToolName);
            Assert.Contains("Plan mode exit requested", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_CreatesListsAndDeletesSessionOnlyCronJobs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-cron-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "yolo"
                  }
                }
                """);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var scheduler = new InMemoryCronScheduler();
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), scheduler);

            var createResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "cron_create",
                ArgumentsJson = """{"cron":"*/5 * * * *","prompt":"check the build"}"""
            });

            var createdJob = scheduler.List().Single();

            var listResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "cron_list",
                ArgumentsJson = "{}"
            });

            var deleteResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "cron_delete",
                ArgumentsJson = $$"""{"id":"{{createdJob.Id}}"}"""
            });

            var emptyListResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "cron_list",
                ArgumentsJson = "{}"
            });

            Assert.Equal("completed", createResult.Status);
            Assert.Contains("Scheduled recurring job", createResult.Output);
            Assert.Contains("Session-only", createResult.Output);
            Assert.Equal("completed", listResult.Status);
            Assert.Contains(createdJob.Id, listResult.Output);
            Assert.Contains("(recurring) [session-only]: check the build", listResult.Output);
            Assert.Equal("completed", deleteResult.Status);
            Assert.Contains($"Cancelled job {createdJob.Id}.", deleteResult.Output);
            Assert.Equal("completed", emptyListResult.Status);
            Assert.Contains("No active cron jobs.", emptyListResult.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_CreatesPendingAskUserQuestionInteraction()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-ask-user-tool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var sourcePaths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService(), new InMemoryCronScheduler());

            var result = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "ask_user_question",
                ArgumentsJson =
                    """
                    {
                      "questions": [
                        {
                          "header": "Library",
                          "question": "Which library should we use?",
                          "multiSelect": false,
                          "options": [
                            { "label": "Spectre", "description": "Keep the current rendering stack." },
                            { "label": "Terminal.Gui", "description": "Switch to a different TUI stack." }
                          ]
                        }
                      ]
                    }
                    """
            });

            Assert.Equal("input-required", result.Status);
            Assert.Equal("ask_user_question", result.ToolName);
            var question = Assert.Single(result.Questions);
            Assert.Equal("Library", question.Header);
            Assert.Equal(2, question.Options.Count);
            Assert.Empty(result.Answers);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NativeToolHostService_ExecuteAsync_BlocksToolWhenPreToolUseHookDeniesExecution()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-pretool-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            string command;
            if (OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(root, "deny-pretool.ps1");
                File.WriteAllText(
                    scriptPath,
                    """
                    [Console]::Error.Write('Tool execution denied by PreToolUse hook')
                    exit 2
                    """);
                command = $"& '{scriptPath.Replace("\\", "\\\\", StringComparison.Ordinal)}'";
            }
            else
            {
                command = CrossPlatformTestSupport.CreateHookCommand(
                    root,
                    "deny-pretool",
                    string.Empty,
                    """
                    printf '%s' 'Tool execution denied by PreToolUse hook' >&2
                    exit 2
                    """);
            }
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                $$"""
                {
                  "permissions": {
                    "defaultMode": "yolo"
                  },
                  "hooksConfig": {
                    "enabled": true
                  },
                  "hooks": {
                    "PreToolUse": [
                      {
                        "hooks": [
                          {
                            "type": "command",
                            "name": "deny-write",
                            "command": "{{command}}"
                          }
                        ]
                      }
                    ]
                  }
                }
                """);

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, systemRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var hookLifecycleService = new HookLifecycleService(
                new HookRegistryService(environmentPaths),
                new HookCommandRunner(),
                new HookOutputAggregator());
            var host = new NativeToolHostService(
                runtimeProfileService,
                new ApprovalPolicyService(),
                new InMemoryCronScheduler(),
                hookLifecycleService: hookLifecycleService);

            var targetFile = Path.Combine(workspaceRoot, "blocked.txt");
            var result = await host.ExecuteAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ExecuteNativeToolRequest
                {
                    ToolName = "write_file",
                    ArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"should-not-be-written"}"""
                });

            Assert.Equal("blocked", result.Status);
            Assert.Contains("PreToolUse", result.ErrorMessage);
            Assert.False(File.Exists(targetFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TimeoutShellExecutionService : IShellExecutionService
    {
        public Task<ShellCommandExecutionResult> ExecuteAsync(
            ShellCommandRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ShellCommandExecutionResult
            {
                WorkingDirectory = request.WorkingDirectory,
                Output = string.Empty,
                ErrorMessage = "Shell command timed out after 100 ms.",
                ExitCode = -1,
                TimedOut = true,
                Cancelled = false
            });
    }
}

file sealed class ThrowingWebToolService(Exception exception) : IWebToolService
{
    public Task<string> FetchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default) =>
        Task.FromException<string>(exception);

    public Task<string> SearchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken = default) =>
        Task.FromException<string>(exception);
}
