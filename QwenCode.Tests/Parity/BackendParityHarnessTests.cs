using QwenCode.App.Ide;

namespace QwenCode.Tests.Parity;

public sealed class BackendParityHarnessTests
{
    [Fact]
    public void UpstreamIntegrationFixtures_AreAvailableForParityHarness()
    {
        var root = @"D:\Projects\qwen-code-main\integration-tests";

        Assert.True(Directory.Exists(root));
        Assert.True(File.Exists(Path.Combine(root, "cli", "settings-migration.test.ts")));
        Assert.True(File.Exists(Path.Combine(root, "hook-integration", "hooks.test.ts")));
        Assert.True(File.Exists(Path.Combine(root, "sdk-typescript", "single-turn.test.ts")));
    }

    [Fact]
    public void IdeWorkspaceValidation_MatchesExpectedParityShape()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"qwen-parity-workspace-{Guid.NewGuid():N}");
        var nestedRoot = Path.Combine(workspaceRoot, "nested");
        Directory.CreateDirectory(nestedRoot);

        try
        {
            var valid = IdeBackendService.ValidateWorkspacePath(workspaceRoot, nestedRoot);
            var invalid = IdeBackendService.ValidateWorkspacePath(Path.Combine(workspaceRoot, "other"), nestedRoot);

            Assert.True(valid.IsValid);
            Assert.False(invalid.IsValid);
            Assert.Contains("Directory mismatch", invalid.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }
}
