namespace QwenCode.Tests.Runtime;

public sealed class ProjectSummaryServiceTests
{
    [Fact]
    public void Read_WhenProjectSummaryExists_ReturnsParsedSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-project-summary-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var summaryDirectory = Path.Combine(workspaceRoot, ".qwen");
        Directory.CreateDirectory(summaryDirectory);

        try
        {
            var summaryPath = Path.Combine(summaryDirectory, "PROJECT_SUMMARY.md");
            File.WriteAllText(
                summaryPath,
                """
                **Update time**: 2026-04-02T10:15:00Z

                ## Overall Goal
                Ship the native desktop runtime.

                ## Current Plan
                1. [DONE] Remove source mirrors from product code.
                2. [IN PROGRESS] Implement reconnect after renderer reload.
                3. [TODO] Parse PROJECT_SUMMARY.md in C#.
                4. [TODO] Add welcome-back payload to bootstrap.
                """
            );

            var snapshot = new ProjectSummaryService().Read(CreateRuntimeProfile(workspaceRoot, trusted: true));

            Assert.NotNull(snapshot);
            Assert.True(snapshot!.HasHistory);
            Assert.Equal(summaryPath, snapshot.FilePath);
            Assert.Equal("2026-04-02T10:15:00Z", snapshot.TimestampText);
            Assert.Equal("Ship the native desktop runtime.", snapshot.OverallGoal);
            Assert.Equal(4, snapshot.TotalTasks);
            Assert.Equal(1, snapshot.DoneCount);
            Assert.Equal(1, snapshot.InProgressCount);
            Assert.Equal(2, snapshot.TodoCount);
            Assert.Equal(3, snapshot.PendingTasks.Count);
            Assert.Contains("[IN PROGRESS] Implement reconnect after renderer reload.", snapshot.PendingTasks);
            Assert.Contains("[TODO] Parse PROJECT_SUMMARY.md in C#.", snapshot.PendingTasks);
            Assert.Contains("[TODO] Add welcome-back payload to bootstrap.", snapshot.PendingTasks);
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
    public void Read_WhenWorkspaceIsUntrusted_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-project-summary-untrusted-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var summaryDirectory = Path.Combine(workspaceRoot, ".qwen");
        Directory.CreateDirectory(summaryDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(summaryDirectory, "PROJECT_SUMMARY.md"),
                """
                ## Overall Goal
                Do not load this in untrusted mode.
                """);

            var snapshot = new ProjectSummaryService().Read(CreateRuntimeProfile(workspaceRoot, trusted: false));

            Assert.Null(snapshot);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static QwenRuntimeProfile CreateRuntimeProfile(string workspaceRoot, bool trusted) =>
        new()
        {
            ProjectRoot = workspaceRoot,
            GlobalQwenDirectory = Path.Combine(Path.GetTempPath(), "qwen-tests-home"),
            RuntimeBaseDirectory = Path.Combine(workspaceRoot, ".qwen-runtime"),
            RuntimeSource = "test",
            ProjectDataDirectory = Path.Combine(workspaceRoot, ".qwen-runtime", "project"),
            ChatsDirectory = Path.Combine(workspaceRoot, ".qwen-runtime", "project", "chats"),
            HistoryDirectory = Path.Combine(workspaceRoot, ".qwen-runtime", "history"),
            ContextFileNames = ["QWEN.md", "AGENTS.md"],
            ContextFilePaths = [],
            FolderTrustEnabled = true,
            IsWorkspaceTrusted = trusted,
            WorkspaceTrustSource = trusted ? "file" : string.Empty,
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
}
