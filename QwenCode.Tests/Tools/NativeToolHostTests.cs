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
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService());

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
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService());

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
            var host = new NativeToolHostService(runtimeProfileService, new ApprovalPolicyService());

            var allowedShellRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"more docs\\guide.txt"}"""
            });

            var gatedShellWrite = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"echo hello > src/output.txt"}"""
            });

            var deniedShellRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"cat .env"}"""
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


}
