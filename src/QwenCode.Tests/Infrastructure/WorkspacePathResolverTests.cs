using QwenCode.Core.Infrastructure;

namespace QwenCode.Tests.Infrastructure;

public sealed class WorkspacePathResolverTests
{
    [Fact]
    public void Resolve_UsesNearestAncestorWithGitMarker()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-workspace-resolver-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        var nested = Path.Combine(workspaceRoot, "src", "feature");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".git"));
        Directory.CreateDirectory(nested);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(root, null, nested, nested);
            var resolver = new WorkspacePathResolver(environmentPaths);

            var resolved = resolver.Resolve(new WorkspacePaths());

            Assert.Equal(workspaceRoot, resolved.WorkspaceRoot);
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
    public void Resolve_UsesExplicitWorkspaceBeforeMarkerDiscovery()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-workspace-explicit-{Guid.NewGuid():N}");
        var configuredWorkspace = Path.Combine(root, "configured");
        var current = Path.Combine(root, "current", "nested");
        Directory.CreateDirectory(configuredWorkspace);
        Directory.CreateDirectory(Path.Combine(root, "current", ".git"));
        Directory.CreateDirectory(current);

        try
        {
            var environmentPaths = new FakeDesktopEnvironmentPaths(root, null, current, current);
            var resolver = new WorkspacePathResolver(environmentPaths);

            var resolved = resolver.Resolve(new WorkspacePaths
            {
                WorkspaceRoot = configuredWorkspace
            });

            Assert.Equal(configuredWorkspace, resolved.WorkspaceRoot);
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
