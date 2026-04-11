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
            Assert.Contains(snapshot.Tools, tool => tool.Name == "edit" && tool.ApprovalState == "ask" && tool.IsExplicitAskRule);
            Assert.Contains(snapshot.Tools, tool => tool.Name == "run_shell_command" && tool.ApprovalState == "deny" && !tool.IsEnabled);
            Assert.Contains(snapshot.Tools, tool => tool.Name == "read_file" && tool.SourcePath == "native://tools/read_file");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "todo_write" && tool.SourcePath == "native://tools/todo_write");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "task_create" && tool.SourcePath == "native://tools/task_create");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "task_list" && tool.SourcePath == "native://tools/task_list");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "task_get" && tool.SourcePath == "native://tools/task_get");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "task_update" && tool.SourcePath == "native://tools/task_update");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "task_stop" && tool.SourcePath == "native://tools/task_stop");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "save_memory" && tool.SourcePath == "native://tools/save_memory");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "agent" && tool.SourcePath == "native://tools/agent");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "skill" && tool.SourcePath == "native://tools/skill");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "tool_search" && tool.SourcePath == "native://tools/tool_search");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "exit_plan_mode" && tool.SourcePath == "native://tools/exit_plan_mode");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "web_fetch" && tool.SourcePath == "native://tools/web_fetch");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "web_search" && tool.SourcePath == "native://tools/web_search");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "mcp-client" && tool.SourcePath == "native://tools/mcp-client");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "mcp-tool" && tool.SourcePath == "native://tools/mcp-tool");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "lsp" && tool.SourcePath == "native://tools/lsp");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "ask_user_question" && tool.SourcePath == "native://tools/ask_user_question");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "cron_create" && tool.SourcePath == "native://tools/cron_create");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "cron_list" && tool.SourcePath == "native://tools/cron_list");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "cron_delete" && tool.SourcePath == "native://tools/cron_delete");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
