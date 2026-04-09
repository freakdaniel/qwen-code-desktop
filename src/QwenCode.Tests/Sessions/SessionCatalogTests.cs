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

            var catalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessions = catalog.ListSessions(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var session = Assert.Single(sessions);

            Assert.Equal("12345678-1234-1234-1234-1234567890ab", session.SessionId);
            Assert.Null(session.Title);
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

            var catalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
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
    public void DesktopSessionCatalogService_GetSession_UsesPreviewTranscriptPathWhenMetadataSessionIdDiffersFromFileName()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-mismatch-{Guid.NewGuid():N}");
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
            var recordingService = new ChatRecordingService();
            var profile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(profile.ChatsDirectory);

            var visibleTranscriptPath = Path.Combine(profile.ChatsDirectory, "visible-chat.jsonl");
            File.WriteAllText(
                visibleTranscriptPath,
                """
                {"uuid":"u-1","parentUuid":null,"sessionId":"visible-chat","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"This is the visible transcript body"}]}}
                """ + Environment.NewLine);
            File.WriteAllText(
                recordingService.GetMetadataPath(visibleTranscriptPath),
                """
                {
                  "sessionId": "alias-session",
                  "transcriptPath": "TRANSCRIPT_PATH",
                  "metadataPath": "METADATA_PATH",
                  "title": "Visible chat title",
                  "workingDirectory": "D:\\demo",
                  "gitBranch": "main",
                  "status": "resume-ready",
                  "startedAt": "2026-04-01T12:00:00Z",
                  "lastUpdatedAt": "2026-04-01T12:05:00Z",
                  "lastCompletedUuid": "u-1",
                  "messageCount": 1,
                  "entryCount": 1
                }
                """
                    .Replace("TRANSCRIPT_PATH", visibleTranscriptPath.Replace("\\", "\\\\"))
                    .Replace("METADATA_PATH", recordingService.GetMetadataPath(visibleTranscriptPath).Replace("\\", "\\\\")));
            File.SetLastWriteTimeUtc(visibleTranscriptPath, DateTime.UtcNow);

            var aliasTranscriptPath = Path.Combine(profile.ChatsDirectory, "alias-session.jsonl");
            File.WriteAllText(
                aliasTranscriptPath,
                """
                {"uuid":"u-1","parentUuid":null,"sessionId":"alias-session","timestamp":"2026-04-01T11:00:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Wrong random transcript"}]}}
                """ + Environment.NewLine);
            File.SetLastWriteTimeUtc(aliasTranscriptPath, DateTime.UtcNow.AddMinutes(-10));

            var catalog = new DesktopSessionCatalogService(runtimeProfileService, recordingService);
            var detail = catalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = "alias-session"
                });

            Assert.NotNull(detail);
            Assert.Equal("alias-session", detail!.Session.SessionId);
            Assert.Equal(visibleTranscriptPath, detail.TranscriptPath);
            Assert.Equal("Visible chat title", detail.Session.Title);
            Assert.Contains("This is the visible transcript body", detail.Entries[0].Body);
            Assert.DoesNotContain("Wrong random transcript", detail.Entries[0].Body);
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

            var catalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
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

    [Fact]
    public void DesktopSessionCatalogService_ListSessions_PrefersRecordedMetadataWhenAvailable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-metadata-{Guid.NewGuid():N}");
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
            var recordingService = new ChatRecordingService();
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "metadata-session.jsonl");
            File.WriteAllText(
                transcriptPath,
                """
                {"uuid":"u-1","parentUuid":null,"sessionId":"metadata-session","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"feature/chat-recording","message":{"role":"user","parts":[{"text":"Original transcript title"}]}}
                """ + Environment.NewLine);

            File.WriteAllText(
                recordingService.GetMetadataPath(transcriptPath),
                """
                {
                  "sessionId": "metadata-session",
                  "transcriptPath": "TRANSCRIPT_PATH",
                  "metadataPath": "METADATA_PATH",
                  "title": "Recorded metadata title",
                  "workingDirectory": "D:\\demo",
                  "gitBranch": "feature/chat-recording",
                  "status": "resume-ready",
                  "startedAt": "2026-04-01T12:00:00Z",
                  "lastUpdatedAt": "2026-04-01T12:05:00Z",
                  "lastCompletedUuid": "u-1",
                  "messageCount": 7,
                  "entryCount": 12
                }
                """
                    .Replace("TRANSCRIPT_PATH", transcriptPath.Replace("\\", "\\\\"))
                    .Replace("METADATA_PATH", recordingService.GetMetadataPath(transcriptPath).Replace("\\", "\\\\")));

            var catalog = new DesktopSessionCatalogService(runtimeProfileService, recordingService);
            var session = Assert.Single(catalog.ListSessions(new WorkspacePaths { WorkspaceRoot = workspaceRoot }));

            Assert.Equal("Recorded metadata title", session.Title);
            Assert.Equal("2026-04-01T12:00:00Z", session.StartedAt);
            Assert.Equal("2026-04-01T12:05:00Z", session.LastUpdatedAt);
            Assert.Equal("2026-04-01T12:05:00Z", session.LastActivity);
            Assert.Equal(7, session.MessageCount);
            Assert.Equal(recordingService.GetMetadataPath(transcriptPath), session.MetadataPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DesktopSessionCatalogService_LoadLastSession_ReturnsMostRecentSession()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-last-{Guid.NewGuid():N}");
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

            var olderPath = Path.Combine(profile.ChatsDirectory, "older-session.jsonl");
            var newerPath = Path.Combine(profile.ChatsDirectory, "newer-session.jsonl");
            File.WriteAllText(olderPath, """{"uuid":"u-1","parentUuid":null,"sessionId":"older-session","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Older session"}]}}""" + Environment.NewLine);
            File.WriteAllText(newerPath, """{"uuid":"u-1","parentUuid":null,"sessionId":"newer-session","timestamp":"2026-04-01T12:05:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Newer session"}]}}""" + Environment.NewLine);
            File.SetLastWriteTimeUtc(olderPath, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(newerPath, DateTime.UtcNow.AddMinutes(-1));

            var catalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());

            var latest = catalog.LoadLastSession(new WorkspacePaths { WorkspaceRoot = workspaceRoot });

            Assert.NotNull(latest);
            Assert.Equal("newer-session", latest!.SessionId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DesktopSessionCatalogService_SessionExists_AndRemoveSession_DeleteTranscriptAndMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-remove-{Guid.NewGuid():N}");
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
            var recordingService = new ChatRecordingService();
            var profile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(profile.ChatsDirectory);

            var transcriptPath = Path.Combine(profile.ChatsDirectory, "remove-session.jsonl");
            File.WriteAllText(transcriptPath, """{"uuid":"u-1","parentUuid":null,"sessionId":"remove-session","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"D:\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Remove me"}]}}""" + Environment.NewLine);
            File.WriteAllText(recordingService.GetMetadataPath(transcriptPath), """{"sessionId":"remove-session","title":"Remove me","workingDirectory":"D:\\demo","gitBranch":"main","status":"resume-ready","startedAt":"2026-04-01T12:00:00Z","lastUpdatedAt":"2026-04-01T12:01:00Z","messageCount":1,"entryCount":1}""");

            var catalog = new DesktopSessionCatalogService(runtimeProfileService, recordingService);
            var paths = new WorkspacePaths { WorkspaceRoot = workspaceRoot };

            Assert.True(catalog.SessionExists(paths, "remove-session"));

            var removed = catalog.RemoveSession(paths, "remove-session");

            Assert.True(removed);
            Assert.False(File.Exists(transcriptPath));
            Assert.False(File.Exists(recordingService.GetMetadataPath(transcriptPath)));
            Assert.False(catalog.SessionExists(paths, "remove-session"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}

