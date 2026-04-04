using QwenCode.App.Output;

namespace QwenCode.Tests.Output;

public sealed class SessionExportServiceTests
{
    [Fact]
    public void BuildSessionSnapshot_AndFormatSession_ReturnsStructuredOutputs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);
            var escapedWorkspaceRoot = workspaceRoot.Replace("\\", "\\\\", StringComparison.Ordinal);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "export-session.jsonl");
            File.WriteAllLines(
                transcriptPath,
                [
                    """
                    {"sessionId":"export-session","timestamp":"2026-04-04T12:00:00Z","type":"user","uuid":"u1","cwd":"WORKSPACE","message":{"parts":[{"text":"Review the provider configuration."}]}}
                    """.Replace("WORKSPACE", escapedWorkspaceRoot, StringComparison.Ordinal),
                    """
                    {"sessionId":"export-session","timestamp":"2026-04-04T12:00:01Z","type":"assistant","uuid":"a1","cwd":"WORKSPACE","message":{"parts":[{"text":"The provider configuration resolves from merged settings."}]}}
                    """.Replace("WORKSPACE", escapedWorkspaceRoot, StringComparison.Ordinal),
                    """
                    {"sessionId":"export-session","timestamp":"2026-04-04T12:00:02Z","type":"tool","uuid":"t1","cwd":"WORKSPACE","toolName":"read_file","status":"completed","output":"Loaded settings.json"}
                    """.Replace("WORKSPACE", escapedWorkspaceRoot, StringComparison.Ordinal)
                ]);

            var service = new SessionExportService(
                transcriptStore,
                new OutputFormatter(
                    new TextOutputFormatter(),
                    new JsonOutputFormatter()));

            var snapshot = service.BuildSessionSnapshot(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest { SessionId = "export-session" });

            Assert.NotNull(snapshot);
            Assert.Equal("export-session", snapshot!.Session.SessionId);
            Assert.Equal(3, snapshot.EntryCount);
            Assert.Equal(3, snapshot.Entries.Count);

            var text = service.FormatSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest { SessionId = "export-session" },
                OutputFormat.Text);
            var json = service.FormatSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest { SessionId = "export-session" },
                OutputFormat.Json);

            Assert.Contains("Session: Review the provider configuration.", text);
            Assert.Contains("Entries:", text);
            Assert.Contains("\"sessionId\": \"export-session\"", json);
            Assert.Contains("\"entryCount\": 3", json);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
