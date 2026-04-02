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

            var snapshot = new ProjectSummaryService().Read(workspaceRoot);

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
}
