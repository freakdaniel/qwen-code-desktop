namespace QwenCode.Tests.Permissions;

public sealed class ApprovalPolicyTests
{
    [Fact]
    public void ApprovalPolicyService_Evaluate_UsesQwenStyleSpecifiersAndMetaCategories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-approvals-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectRoot = Path.Combine(root, "workspace");
            var docsRoot = Path.Combine(projectRoot, "docs");
            var srcRoot = Path.Combine(projectRoot, "src");
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);

            var service = new ApprovalPolicyService();
            var profile = new ApprovalProfile
            {
                DefaultMode = "default",
                AllowRules = ["Bash(git *)", "Read(./docs/**)"],
                AskRules = ["Edit(/src/**)"],
                DenyRules = ["WebFetch(domain:example.com)"]
            };

            var shellDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "git status"
                },
                profile);

            var readDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "grep_search",
                    Kind = "read",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    FilePath = Path.Combine(docsRoot, "guide.md")
                },
                profile);

            var editDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "write_file",
                    Kind = "modify",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    FilePath = Path.Combine(srcRoot, "Program.cs")
                },
                profile);

            var webDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "web_fetch",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Domain = "api.example.com"
                },
                profile);

            Assert.Equal("allow", shellDecision.State);
            Assert.Contains("Bash(git *)", shellDecision.Reason);
            Assert.Equal("allow", readDecision.State);
            Assert.Contains("Read(./docs/**)", readDecision.Reason);
            Assert.Equal("ask", editDecision.State);
            Assert.Contains("Edit(/src/**)", editDecision.Reason);
            Assert.Equal("deny", webDecision.State);
            Assert.Contains("WebFetch(domain:example.com)", webDecision.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApprovalPolicyService_Evaluate_AppliesVirtualShellOperations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-virtual-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectRoot = Path.Combine(root, "workspace");
            var docsRoot = Path.Combine(projectRoot, "docs");
            var srcRoot = Path.Combine(projectRoot, "src");
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);

            var service = new ApprovalPolicyService();
            var profile = new ApprovalProfile
            {
                DefaultMode = "plan",
                AllowRules = ["Read(./docs/**)"],
                AskRules = ["Edit(/src/**)"],
                DenyRules = ["Read(.env)"]
            };

            var readDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = CrossPlatformTestSupport.GetReadFileShellCommand(
                        OperatingSystem.IsWindows() ? @"docs\guide.md" : "docs/guide.md")
                },
                profile);

            var editDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = CrossPlatformTestSupport.GetWriteFileShellCommand(
                        OperatingSystem.IsWindows() ? @"src\output.txt" : "src/output.txt",
                        "hello")
                },
                profile);

            var denyDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = CrossPlatformTestSupport.GetReadFileShellCommand(".env")
                },
                profile);

            Assert.Equal("allow", readDecision.State);
            Assert.Contains("shell semantics", readDecision.Reason);
            Assert.Equal("ask", editDecision.State);
            Assert.Contains("Edit(/src/**)", editDecision.Reason);
            Assert.Equal("deny", denyDecision.State);
            Assert.Contains("Read(.env)", denyDecision.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
