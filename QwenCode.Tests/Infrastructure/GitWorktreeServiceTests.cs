using QwenCode.App.Infrastructure;

namespace QwenCode.Tests.Infrastructure;

public sealed class GitWorktreeServiceTests
{
    [Fact]
    public void Inspect_DiscoversManagedWorktrees()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-worktrees-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");
        var managedWorktreePath = Path.Combine(homeRoot, ".qwen", "worktrees", "session-123", "worktrees", "code-review");

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

            Directory.CreateDirectory(Path.GetDirectoryName(managedWorktreePath)!);
            RunGit(workspaceRoot, "worktree", "add", "-b", "main-session-123-code-review", managedWorktreePath, "HEAD");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var service = new GitWorktreeService(new GitCliService(), runtimeProfileService);

            var snapshot = service.Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            });

            Assert.True(snapshot.IsGitAvailable);
            Assert.True(snapshot.IsRepository);
            Assert.True(snapshot.WorktreeSupported);
            Assert.Equal(1, snapshot.ManagedSessionCount);
            Assert.Contains(snapshot.Worktrees, item => item.IsCurrent && !item.IsManaged);
            Assert.Contains(snapshot.Worktrees, item =>
                item.IsManaged &&
                item.SessionId == "session-123" &&
                item.Name == "code-review");
        }
        finally
        {
            TryRunGit(workspaceRoot, "worktree", "remove", "--force", managedWorktreePath);
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public void CreateAndCleanupManagedSession_UsesQwenWorktreeLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-worktree-lifecycle-{Guid.NewGuid():N}");
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

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var service = new GitWorktreeService(new GitCliService(), runtimeProfileService);
            var paths = new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            };

            var created = service.CreateManagedWorktree(paths, new CreateManagedWorktreeRequest
            {
                SessionId = "session-lifecycle",
                Name = "feature-a",
                BaseBranch = "main"
            });

            Assert.Contains(created.Worktrees, item =>
                item.IsManaged &&
                item.SessionId == "session-lifecycle" &&
                item.Name == "feature-a");

            var sessionConfigPath = Path.Combine(homeRoot, ".qwen", "worktrees", "session-lifecycle", "config.json");
            Assert.True(File.Exists(sessionConfigPath));

            var cleaned = service.CleanupManagedSession(paths, new CleanupManagedWorktreeSessionRequest
            {
                SessionId = "session-lifecycle"
            });

            Assert.DoesNotContain(cleaned.Worktrees, item => string.Equals(item.SessionId, "session-lifecycle", StringComparison.OrdinalIgnoreCase));
            Assert.False(Directory.Exists(Path.Combine(homeRoot, ".qwen", "worktrees", "session-lifecycle")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    [Fact]
    public void CreateManagedWorktree_OverlaysDirtyStateAndCreatesBaselineCommit()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-worktree-overlay-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            File.WriteAllText(Path.Combine(workspaceRoot, "tracked.txt"), "original");

            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            File.WriteAllText(Path.Combine(workspaceRoot, "tracked.txt"), "dirty tracked content");
            File.WriteAllText(Path.Combine(workspaceRoot, "untracked.txt"), "dirty untracked content");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var service = new GitWorktreeService(new GitCliService(), runtimeProfileService);
            var paths = new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            };

            var created = service.CreateManagedWorktree(paths, new CreateManagedWorktreeRequest
            {
                SessionId = "overlay-session",
                Name = "overlay-branch",
                BaseBranch = "main"
            });

            var worktree = Assert.Single(created.Worktrees, item => item.SessionId == "overlay-session");
            Assert.Equal("dirty tracked content", File.ReadAllText(Path.Combine(worktree.Path, "tracked.txt")));
            Assert.Equal("dirty untracked content", File.ReadAllText(Path.Combine(worktree.Path, "untracked.txt")));

            var baselineMessage = new GitCliService().Run(worktree.Path, "log", "-1", "--pretty=%s");
            Assert.True(baselineMessage.Success, baselineMessage.StandardError);
            Assert.Equal(GitWorktreeService.BaselineCommitMessage, baselineMessage.StandardOutput.Trim());

            var sourceStatus = new GitCliService().Run(workspaceRoot, "status", "--short");
            Assert.True(sourceStatus.Success, sourceStatus.StandardError);
            Assert.Contains("M tracked.txt", sourceStatus.StandardOutput);
            Assert.Contains("?? untracked.txt", sourceStatus.StandardOutput);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                DeleteDirectory(root);
            }
        }
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var result = new GitCliService().Run(workingDirectory, arguments);
        Assert.True(result.Success, result.StandardError);
    }

    private static void TryRunGit(string workingDirectory, params string[] arguments)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return;
        }

        _ = new GitCliService().Run(workingDirectory, arguments);
    }

    private static void DeleteDirectory(string root)
    {
        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }
}
