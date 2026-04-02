namespace QwenCode.Tests.Sessions;

public sealed class SessionHostApprovalTests
{
    [Fact]
    public async Task DesktopSessionHostService_ApprovePendingToolAsync_ExecutesStoredToolAndResolvesPendingEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-approve-pending-tool-{Guid.NewGuid():N}");
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
                    "ask": ["Edit"]
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var startResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a pending edit for approval.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"approved write"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(pendingDetail);
            var pendingToolEntries = pendingDetail!.Entries.Where(entry => entry.Type == "tool").ToArray();
            var pendingToolEntry = Assert.Single(pendingToolEntries);

            var approvalResult = await sessionHost.ApprovePendingToolAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ApproveDesktopSessionToolRequest
                {
                    SessionId = startResult.Session.SessionId,
                    EntryId = pendingToolEntry.Id
                });

            Assert.Equal("completed", approvalResult.ToolExecution.Status);
            Assert.Equal("write_file", approvalResult.ToolExecution.ToolName);
            Assert.Equal("approved write", File.ReadAllText(targetFile));

            var finalDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(finalDetail);
            Assert.Equal(0, finalDetail!.Summary.PendingApprovalCount);
            Assert.Equal(1, finalDetail.Summary.CompletedToolCount);

            var resolvedPendingEntry = finalDetail.Entries.First(entry => entry.Id == pendingToolEntry.Id);
            Assert.Equal("approved", resolvedPendingEntry.ResolutionStatus);
            Assert.False(string.IsNullOrWhiteSpace(resolvedPendingEntry.ResolvedAt));

            var completedExecutionEntry = finalDetail.Entries.Last(entry =>
                entry.Type == "tool" &&
                entry.ToolName == "write_file" &&
                entry.Status == "completed");
            Assert.Contains("approved write", completedExecutionEntry.Arguments);
            Assert.Equal("executed-after-approval", completedExecutionEntry.ResolutionStatus);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_ApprovePendingToolAsync_EmitsLifecycleEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-approve-events-{Guid.NewGuid():N}");
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
                    "ask": ["Edit"]
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var startResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a pending edit for event emission.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"approved write"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(pendingDetail);
            var pendingToolEntry = pendingDetail!.Entries.Last(entry =>
                entry.Type == "tool" &&
                entry.ToolName == "write_file" &&
                entry.Status == "approval-required");

            var emittedEvents = new List<DesktopSessionEvent>();
            sessionHost.SessionEvent += (_, sessionEvent) => emittedEvents.Add(sessionEvent);

            await sessionHost.ApprovePendingToolAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ApproveDesktopSessionToolRequest
                {
                    SessionId = startResult.Session.SessionId,
                    EntryId = pendingToolEntry.Id
                });

            Assert.Collection(
                emittedEvents.Select(item => item.Kind),
                kind => Assert.Equal(DesktopSessionEventKind.ToolApproved, kind),
                kind => Assert.Equal(DesktopSessionEventKind.ToolCompleted, kind),
                kind => Assert.Equal(DesktopSessionEventKind.AssistantPreparingContext, kind),
                kind => Assert.Equal(DesktopSessionEventKind.AssistantGenerating, kind),
                kind => Assert.Equal(DesktopSessionEventKind.AssistantCompleted, kind),
                kind => Assert.Equal(DesktopSessionEventKind.TurnCompleted, kind));

            Assert.All(emittedEvents, item => Assert.Equal(startResult.Session.SessionId, item.SessionId));
            Assert.Contains(emittedEvents, item => item.ToolName == "write_file");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}
