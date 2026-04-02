namespace QwenCode.Tests.Tools;

public sealed class ToolCatalogTests
{
    [Fact]
    public void ToolCatalogService_Inspect_ParsesToolsAndAppliesApprovalProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-tools-{Guid.NewGuid():N}");
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
                    "ask": ["Edit"],
                    "deny": ["Bash"]
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var catalog = new ToolCatalogService(runtimeProfileService, new ApprovalPolicyService());
            var snapshot = catalog.Inspect(new WorkspacePaths
            {
                WorkspaceRoot = workspaceRoot
            });

            Assert.Equal("native-contracts", snapshot.SourceMode);
            Assert.True(snapshot.TotalCount >= 7);
            Assert.Contains(snapshot.Tools, tool => tool.Name == "read_file" && tool.ApprovalState == "allow");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "edit" && tool.ApprovalState == "ask");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "run_shell_command" && tool.ApprovalState == "deny");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "read_file" && tool.SourcePath == "native://tools/read_file");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
