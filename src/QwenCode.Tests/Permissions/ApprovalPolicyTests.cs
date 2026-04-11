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
            Assert.True(editDecision.IsExplicitAskRule);
            Assert.Equal("deny", denyDecision.State);
            Assert.Contains("Read(.env)", denyDecision.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApprovalPolicyService_Evaluate_ExposesExplicitAskAndWholeToolDenyMetadata()
    {
        var service = new ApprovalPolicyService();
        var profile = new ApprovalProfile
        {
            DefaultMode = "default",
            AllowRules = [],
            AskRules = ["Edit"],
            DenyRules = ["Bash"]
        };

        var askDecision = service.Evaluate(
            new ApprovalCheckContext
            {
                ToolName = "edit",
                Kind = "modify"
            },
            profile);

        var denyDecision = service.Evaluate(
            new ApprovalCheckContext
            {
                ToolName = "run_shell_command",
                Kind = "execute"
            },
            profile);

        Assert.Equal("ask", askDecision.State);
        Assert.True(askDecision.IsExplicitAskRule);
        Assert.Equal("Edit", askDecision.MatchedRule);
        Assert.Equal("deny", denyDecision.State);
        Assert.True(denyDecision.IsWholeToolDenyRule);
        Assert.Equal("Bash", denyDecision.MatchedRule);
    }

    [Fact]
    public void ApprovalPolicyService_Evaluate_CombinesCompoundShellSegmentsByMostRestrictiveDecision()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-compound-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectRoot = Path.Combine(root, "workspace");
            Directory.CreateDirectory(projectRoot);

            var service = new ApprovalPolicyService();
            var profile = new ApprovalProfile
            {
                DefaultMode = "default",
                AllowRules = ["Bash(git *)"],
                AskRules = [],
                DenyRules = []
            };

            var readOnlyDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "cd docs && git status"
                },
                profile);

            var mutatingDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "rm temp.txt && git status"
                },
                profile);

            Assert.Equal("allow", readOnlyDecision.State);
            Assert.Equal("ask", mutatingDecision.State);
            Assert.Contains("rm temp.txt", mutatingDecision.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ApprovalPolicyService_Evaluate_DeniesCommandSubstitutionAndDetectsWebDownloads()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-safety-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectRoot = Path.Combine(root, "workspace");
            var srcRoot = Path.Combine(projectRoot, "src");
            Directory.CreateDirectory(srcRoot);

            var service = new ApprovalPolicyService();
            var profile = new ApprovalProfile
            {
                DefaultMode = "default",
                AllowRules = [],
                AskRules = [],
                DenyRules = ["Write(/src/**)"]
            };

            var substitutionDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "echo $(whoami)"
                },
                profile);

            var downloadDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "curl -o src/out.txt https://example.com/archive.txt"
                },
                profile);

            Assert.Equal("deny", substitutionDecision.State);
            Assert.Contains("command substitution", substitutionDecision.Reason);
            Assert.Equal("deny", downloadDecision.State);
            Assert.Contains("Write(/src/**)", downloadDecision.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

}
