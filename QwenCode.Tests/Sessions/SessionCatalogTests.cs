namespace QwenCode.Tests.Sessions;

public sealed class SessionCatalogTests
{
    [Fact]
    public void DesktopSessionCatalogService_ListSessions_ReadsQwenChatTranscript()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var sessionFilePath = Path.Combine(runtimeProfile.ChatsDirectory, "12345678-1234-1234-1234-1234567890ab.jsonl");
            File.WriteAllLines(
                sessionFilePath,
                [
                    """
                    {"uuid":"u-1","parentUuid":null,"sessionId":"12345678-1234-1234-1234-1234567890ab","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"/workspace/demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Audit the renderer parity and plan the next native host step."}]}}
                    """,
                    """
                    {"uuid":"u-2","parentUuid":"u-1","sessionId":"12345678-1234-1234-1234-1234567890ab","timestamp":"2026-04-01T12:01:00Z","type":"assistant","cwd":"/workspace/demo","version":"0.1.0","gitBranch":"main","message":{"role":"model","parts":[{"text":"I'll inspect the current desktop host."}]}}
                    """
                ]);
            File.SetLastWriteTimeUtc(sessionFilePath, DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));

            var catalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessions = catalog.ListSessions(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var session = Assert.Single(sessions);

            Assert.Equal("12345678-1234-1234-1234-1234567890ab", session.SessionId);
            Assert.Contains("Audit the renderer parity", session.Title);
            Assert.Equal("main", session.Category);
            Assert.Equal(DesktopMode.Code, session.Mode);
            Assert.Equal(2, session.MessageCount);
            Assert.Equal("resume-ready", session.Status);
            Assert.Equal(sessionFilePath, session.TranscriptPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DesktopSessionCatalogService_GetSession_ReadsTranscriptEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-detail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var profile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(profile.ChatsDirectory);

            var sessionFilePath = Path.Combine(profile.ChatsDirectory, "detail-session.jsonl");
            File.WriteAllLines(
                sessionFilePath,
                [
                    """
                    {"uuid":"u-1","parentUuid":null,"sessionId":"detail-session","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"/workspace/demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Inspect transcript detail."}]}}
                    """,
                    """
                    {"uuid":"u-2","parentUuid":"u-1","sessionId":"detail-session","timestamp":"2026-04-01T12:00:10Z","type":"command","cwd":"/workspace/demo","version":"0.1.0","gitBranch":"main","commandName":"context","resolvedPrompt":"Show the current runtime context.","status":"completed","output":"Workspace: /workspace/demo"}
                    """,
                    """
                    {"uuid":"u-3","parentUuid":"u-2","sessionId":"detail-session","timestamp":"2026-04-01T12:00:20Z","type":"tool","cwd":"/workspace/demo","version":"0.1.0","gitBranch":"main","toolName":"read_file","status":"completed","output":"README contents"}
                    """,
                    """
                    {"uuid":"u-4","parentUuid":"u-3","sessionId":"detail-session","timestamp":"2026-04-01T12:00:30Z","type":"assistant","cwd":"/workspace/demo","version":"0.1.0","gitBranch":"main","message":{"role":"assistant","parts":[{"text":"Transcript detail is available."}]}}
                    """
                ]);

            var catalog = new DesktopSessionCatalogService(runtimeProfileService);
            var detail = catalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = "detail-session"
                });

            Assert.NotNull(detail);
            Assert.Equal("detail-session", detail!.Session.SessionId);
            Assert.Equal(4, detail.EntryCount);
            Assert.Equal(1, detail.Summary.UserCount);
            Assert.Equal(1, detail.Summary.AssistantCount);
            Assert.Equal(1, detail.Summary.CommandCount);
            Assert.Equal(1, detail.Summary.ToolCount);
            Assert.Equal(0, detail.Summary.PendingApprovalCount);
            Assert.Collection(
                detail.Entries,
                entry =>
                {
                    Assert.Equal("user", entry.Type);
                    Assert.Equal("User", entry.Title);
                    Assert.Contains("Inspect transcript detail.", entry.Body);
                },
                entry =>
                {
                    Assert.Equal("command", entry.Type);
                    Assert.Equal("/context", entry.Title);
                    Assert.Equal("completed", entry.Status);
                    Assert.Equal("Workspace: /workspace/demo", entry.Body);
                },
                entry =>
                {
                    Assert.Equal("tool", entry.Type);
                    Assert.Equal("read_file", entry.Title);
                    Assert.Contains("README contents", entry.Body);
                    Assert.Equal("completed", entry.Status);
                },
                entry =>
                {
                    Assert.Equal("assistant", entry.Type);
                    Assert.Equal("Assistant", entry.Title);
                    Assert.Contains("Transcript detail is available.", entry.Body);
                });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DesktopSessionCatalogService_GetSession_PaginatesLargeTranscriptWindows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-window-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var profile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(profile.ChatsDirectory);

            var sessionFilePath = Path.Combine(profile.ChatsDirectory, "window-session.jsonl");
            var lines = Enumerable.Range(0, 300)
                .Select(index =>
                    string.Concat(
                        "{\"uuid\":\"u-",
                        index,
                        "\",\"parentUuid\":null,\"sessionId\":\"window-session\",\"timestamp\":\"2026-04-01T12:00:00Z\",\"type\":\"assistant\",\"cwd\":\"/workspace/demo\",\"version\":\"0.1.0\",\"gitBranch\":\"main\",\"message\":{\"role\":\"assistant\",\"parts\":[{\"text\":\"Entry ",
                        index,
                        "\"}]}}"))
                .ToArray();
            File.WriteAllLines(sessionFilePath, lines);

            var catalog = new DesktopSessionCatalogService(runtimeProfileService);
            var detail = catalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = "window-session",
                    Offset = 150,
                    Limit = 25
                });

            Assert.NotNull(detail);
            Assert.Equal(300, detail!.EntryCount);
            Assert.Equal(150, detail.WindowOffset);
            Assert.Equal(25, detail.WindowSize);
            Assert.True(detail.HasOlderEntries);
            Assert.True(detail.HasNewerEntries);
            Assert.Equal(25, detail.Entries.Count);
            Assert.Contains("Entry 150", detail.Entries[0].Body);
            Assert.Contains("Entry 174", detail.Entries[^1].Body);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
