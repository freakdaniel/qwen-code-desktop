using QwenCode.App.Infrastructure;

namespace QwenCode.Tests.Infrastructure;

public sealed class FileDiscoveryServiceTests
{
    [Fact]
    public void Inspect_RespectsGitAndQwenIgnoreRules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-discovery-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var homeRoot = Path.Combine(root, "home");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(homeRoot);

        try
        {
            Directory.CreateDirectory(Path.Combine(workspaceRoot, "src"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs"));
            File.WriteAllText(Path.Combine(workspaceRoot, "QWEN.md"), "Project context");
            File.WriteAllText(Path.Combine(workspaceRoot, ".gitignore"), "*.log");
            File.WriteAllText(Path.Combine(workspaceRoot, ".qwenignore"), "docs/private-*.md");
            File.WriteAllText(Path.Combine(workspaceRoot, "src", "tracked.cs"), "class Tracked {}");
            File.WriteAllText(Path.Combine(workspaceRoot, "notes.txt"), "visible");
            File.WriteAllText(Path.Combine(workspaceRoot, "debug.log"), "git ignored");
            File.WriteAllText(Path.Combine(workspaceRoot, "docs", "private-notes.md"), "qwen ignored");

            RunGit(workspaceRoot, "init", "--initial-branch=main");
            RunGit(workspaceRoot, "config", "user.email", "codex@example.com");
            RunGit(workspaceRoot, "config", "user.name", "Codex");
            RunGit(workspaceRoot, "add", ".");
            RunGit(workspaceRoot, "commit", "-m", "init");

            var environmentPaths = new FakeDesktopEnvironmentPaths(homeRoot, null, workspaceRoot, workspaceRoot);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var service = new FileDiscoveryService(new GitCliService(), runtimeProfileService);

            var snapshot = service.Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            });

            Assert.True(snapshot.GitAware);
            Assert.True(snapshot.HasQwenIgnore);
            Assert.Equal(1, snapshot.QwenIgnorePatternCount);
            Assert.True(snapshot.GitIgnoredCount >= 1);
            Assert.True(snapshot.QwenIgnoredCount >= 1);
            Assert.Contains("QWEN.md", snapshot.ContextFiles);
            Assert.Contains("notes.txt", snapshot.SampleVisibleFiles);
            Assert.Contains("docs/private-notes.md", snapshot.SampleQwenIgnoredFiles);
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

    private static void DeleteDirectory(string root)
    {
        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }
}
