namespace QwenCode.Tests.Sessions;

public sealed class SessionRecoveryTests
{
    [Fact]
    public async Task ResumeInterruptedTurnAsync_UsesStoredSnapshotAndClearsRecoveryMarker()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-recovery-{Guid.NewGuid():N}");
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
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var interruptedTurnStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedTurnStore);
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                sessionCatalog,
                activeTurnRegistry,
                interruptedTurnStore);

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "recover-session.jsonl");
            File.WriteAllText(
                transcriptPath,
                """
                {"uuid":"u-1","parentUuid":null,"sessionId":"recover-session","timestamp":"2026-04-02T10:00:00Z","type":"user","cwd":"E:\\workspace","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Finish the interrupted refactor."}]}}
                """ + Environment.NewLine);

            interruptedTurnStore.Upsert(new ActiveTurnState
            {
                SessionId = "recover-session",
                Prompt = "Finish the interrupted refactor.",
                TranscriptPath = transcriptPath,
                WorkingDirectory = workspaceRoot,
                GitBranch = "main",
                ToolName = "edit",
                Stage = "response-delta",
                Status = "streaming",
                ContentSnapshot = "Partial implementation plan.",
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                LastUpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });

            var result = await sessionHost.ResumeInterruptedTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ResumeInterruptedTurnRequest
                {
                    SessionId = "recover-session",
                    RecoveryNote = "finish cleanup"
                });

            Assert.Equal("recover-session", result.Session.SessionId);
            Assert.False(File.Exists(Path.Combine(runtimeProfile.ChatsDirectory, "recover-session.interrupted.json")));

            var transcript = File.ReadAllText(transcriptPath);
            Assert.Contains("\"type\":\"system\"", transcript);
            Assert.Contains("interrupted", transcript, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Recovery note: finish cleanup", transcript);

            var detail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = "recover-session"
                });

            Assert.NotNull(detail);
            Assert.Contains(detail!.Entries, entry => entry.Type == "system" && entry.Status == "interrupted");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DismissInterruptedTurnAsync_RemovesRecoverableSnapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-dismiss-{Guid.NewGuid():N}");
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
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var interruptedTurnStore = new InterruptedTurnStore();
            var activeTurnRegistry = new ActiveTurnRegistry(interruptedTurnStore);
            var sessionHost = CreateSessionHost(
                runtimeProfileService,
                compatibilityService,
                sessionCatalog,
                activeTurnRegistry,
                interruptedTurnStore);

            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);
            var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "dismiss-session.jsonl");

            interruptedTurnStore.Upsert(new ActiveTurnState
            {
                SessionId = "dismiss-session",
                Prompt = "Dismiss this recoverable turn.",
                TranscriptPath = transcriptPath,
                WorkingDirectory = workspaceRoot,
                GitBranch = "main",
                ToolName = string.Empty,
                Stage = "assistant-generating",
                Status = "streaming",
                ContentSnapshot = string.Empty,
                StartedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                LastUpdatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
            });

            var result = await sessionHost.DismissInterruptedTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new DismissInterruptedTurnRequest
                {
                    SessionId = "dismiss-session"
                });

            Assert.True(result.Dismissed);
            Assert.False(File.Exists(Path.Combine(runtimeProfile.ChatsDirectory, "dismiss-session.interrupted.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

