using QwenCode.App.Infrastructure;

namespace QwenCode.Tests.Infrastructure;

public sealed class GitHistoryServiceTests
{
    [Fact]
    public void Inspect_ReturnsEmptySnapshot_WhenHistoryStoreDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-history-empty-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            var service = CreateService(workspaceRoot, homeRoot);

            var snapshot = service.Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            });

            Assert.False(snapshot.IsInitialized);
            Assert.Equal(0, snapshot.CheckpointCount);
            Assert.Empty(snapshot.CurrentCheckpoint);
            Assert.Empty(snapshot.RecentCheckpoints);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void CreateCheckpoint_InitializesShadowRepositoryAndCreatesCommit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-history-create-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "hello");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "hello history");

            var service = CreateService(workspaceRoot, homeRoot);
            var paths = new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            };

            var snapshot = service.CreateCheckpoint(paths, new CreateGitCheckpointRequest
            {
                Message = "Checkpoint from test"
            });

            Assert.True(snapshot.IsInitialized);
            Assert.True(snapshot.CheckpointCount >= 2);
            Assert.False(string.IsNullOrWhiteSpace(snapshot.CurrentCheckpoint));

            var latestCheckpoint = snapshot.RecentCheckpoints[0];
            Assert.Equal("Checkpoint from test", latestCheckpoint.Message);

            var historyDirectory = snapshot.HistoryDirectory;
            Assert.True(Directory.Exists(Path.Combine(historyDirectory, ".git")));

            var showResult = new GitCliService().Run(
                workspaceRoot,
                $"--git-dir={Path.Combine(historyDirectory, ".git")}",
                $"--work-tree={workspaceRoot}",
                "show",
                "HEAD:README.md");
            Assert.True(showResult.Success, showResult.StandardError);
            Assert.Contains("hello history", showResult.StandardOutput);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void RestoreCheckpoint_RestoresTrackedFilesAndRemovesUntrackedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-history-restore-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "tracked.txt"), "version one");
            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var service = CreateService(workspaceRoot, homeRoot);
            var paths = new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            };

            var initialSnapshot = service.CreateCheckpoint(paths, new CreateGitCheckpointRequest
            {
                Message = "Baseline checkpoint"
            });
            var baselineCommitHash = initialSnapshot.CurrentCheckpoint;

            File.WriteAllText(Path.Combine(workspaceRoot, "tracked.txt"), "version two");
            File.WriteAllText(Path.Combine(workspaceRoot, "ephemeral.txt"), "remove me");

            service.RestoreCheckpoint(paths, new RestoreGitCheckpointRequest
            {
                CommitHash = baselineCommitHash
            });

            Assert.Equal("version one", File.ReadAllText(Path.Combine(workspaceRoot, "tracked.txt")));
            Assert.False(File.Exists(Path.Combine(workspaceRoot, "ephemeral.txt")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static GitHistoryService CreateService(string workspaceRoot, string homeRoot)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot);
        var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
        return new GitHistoryService(new GitCliService(), runtimeProfileService);
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var result = new GitCliService().Run(workingDirectory, arguments);
        Assert.True(result.Success, result.StandardError);
    }

    private static void DeleteDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }
}
