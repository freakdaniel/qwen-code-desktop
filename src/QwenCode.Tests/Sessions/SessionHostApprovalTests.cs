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
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
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
                entry.ToolName == "write_file");
            Assert.Equal("completed", completedExecutionEntry.Status);
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
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
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

    [Fact]
    public async Task DesktopSessionHostService_AnswerPendingQuestionAsync_StoresAnswersAndResolvesPendingEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-answer-question-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var startResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Ask the user which implementation path to take.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "ask_user_question",
                    ToolArgumentsJson =
                        """
                        {
                          "questions": [
                            {
                              "header": "Direction",
                              "question": "Which implementation path should we take?",
                              "multiSelect": false,
                              "options": [
                                { "label": "Native host", "description": "Continue deepening the C# runtime." },
                                { "label": "UI polish", "description": "Pause runtime work and improve the renderer." }
                              ]
                            }
                          ]
                        }
                        """,
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(pendingDetail);
            Assert.Equal(1, pendingDetail!.Summary.PendingQuestionCount);

            var pendingEntry = Assert.Single(pendingDetail.Entries, entry =>
                entry.Type == "tool" &&
                entry.ToolName == "ask_user_question" &&
                entry.Status == "input-required");

            var answerResult = await sessionHost.AnswerPendingQuestionAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new AnswerDesktopSessionQuestionRequest
                {
                    SessionId = startResult.Session.SessionId,
                    EntryId = pendingEntry.Id,
                    Answers =
                    [
                        new DesktopQuestionAnswer
                        {
                            QuestionIndex = 0,
                            Value = "Native host"
                        }
                    ]
                });

            Assert.Equal("completed", answerResult.ToolExecution.Status);
            Assert.Equal("ask_user_question", answerResult.ToolExecution.ToolName);
            Assert.Contains("Native host", answerResult.ToolExecution.Output);

            var finalDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(finalDetail);
            Assert.Equal(0, finalDetail!.Summary.PendingQuestionCount);

            var resolvedPendingEntry = finalDetail.Entries.First(entry => entry.Id == pendingEntry.Id);
            Assert.Equal("answered", resolvedPendingEntry.ResolutionStatus);
            Assert.False(string.IsNullOrWhiteSpace(resolvedPendingEntry.ResolvedAt));

            var completedQuestionEntry = finalDetail.Entries.Last(entry =>
                entry.Type == "tool" &&
                entry.ToolName == "ask_user_question" &&
                entry.Status == "completed");
            Assert.Equal("answered-by-user", completedQuestionEntry.ResolutionStatus);
            Assert.Single(completedQuestionEntry.Answers);
            Assert.Equal("Native host", completedQuestionEntry.Answers[0].Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_ApprovePendingToolAsync_AlwaysAllow_PersistsProjectRuleAndSkipsMatchingFutureApproval()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-approval-rule-{Guid.NewGuid():N}");
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
                    "defaultMode": "default"
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var firstTurn = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a shell approval.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "run_shell_command",
                    ToolArgumentsJson = """{"command":"dotnet help"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = firstTurn.Session.SessionId
                });
            Assert.NotNull(pendingDetail);

            var pendingEntry = Assert.Single(
                pendingDetail!.Entries,
                entry => entry.Type == "tool" && entry.Status == "approval-required");

            await sessionHost.ApprovePendingToolAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ApproveDesktopSessionToolRequest
                {
                    SessionId = firstTurn.Session.SessionId,
                    EntryId = pendingEntry.Id,
                    Decision = "always-allow"
                });

            var settingsPath = Path.Combine(workspaceRoot, ".qwen", "settings.json");
            var settingsJson = await File.ReadAllTextAsync(settingsPath);
            Assert.Contains("Bash(dotnet *)", settingsJson, StringComparison.Ordinal);

            var secondTurn = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Run the same shell family again.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "run_shell_command",
                    ToolArgumentsJson = """{"command":"dotnet help"}""",
                    ApproveToolExecution = false
                });

            Assert.NotEqual("approval-required", secondTurn.ToolExecution.Status);

            var secondDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = secondTurn.Session.SessionId
                });
            Assert.NotNull(secondDetail);
            Assert.Equal(0, secondDetail!.Summary.PendingApprovalCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_ApprovePendingToolAsync_AlwaysAllowSession_SkipsMatchingFutureApprovalOnlyForSession()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-approval-rule-{Guid.NewGuid():N}");
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
                    "defaultMode": "default"
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var firstTurn = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a session-scoped shell approval.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "run_shell_command",
                    ToolArgumentsJson = """{"command":"dotnet help"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = firstTurn.Session.SessionId
                });
            Assert.NotNull(pendingDetail);
            var pendingEntry = Assert.Single(
                pendingDetail!.Entries,
                entry => entry.Type == "tool" && entry.Status == "approval-required");
            var extraPendingEntryId = Guid.NewGuid().ToString();
            await File.AppendAllTextAsync(
                pendingDetail.Session.TranscriptPath,
                JsonSerializer.Serialize(new
                {
                    uuid = extraPendingEntryId,
                    parentUuid = pendingEntry.Id,
                    sessionId = firstTurn.Session.SessionId,
                    timestamp = DateTime.UtcNow,
                    type = "tool",
                    cwd = workspaceRoot,
                    version = "0.1.0",
                    gitBranch = string.Empty,
                    toolName = "run_shell_command",
                    args = """{"command":"dotnet help"}""",
                    approvalState = "ask",
                    status = "approval-required",
                    output = string.Empty,
                    errorMessage = "Approval is required for run_shell_command.",
                    exitCode = 0,
                    changedFiles = Array.Empty<string>()
                }) + Environment.NewLine);

            await sessionHost.ApprovePendingToolAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ApproveDesktopSessionToolRequest
                {
                    SessionId = firstTurn.Session.SessionId,
                    EntryId = pendingEntry.Id,
                    Decision = "always-allow-session"
                });

            var settingsJson = await File.ReadAllTextAsync(Path.Combine(workspaceRoot, ".qwen", "settings.json"));
            Assert.DoesNotContain("Bash(dotnet *)", settingsJson, StringComparison.Ordinal);

            var sameSessionTurn = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    SessionId = firstTurn.Session.SessionId,
                    Prompt = "Run the same shell family in the same session.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "run_shell_command",
                    ToolArgumentsJson = """{"command":"dotnet help"}""",
                    ApproveToolExecution = false
                });

            Assert.NotEqual("approval-required", sameSessionTurn.ToolExecution.Status);
            var finalDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = firstTurn.Session.SessionId
                });
            Assert.NotNull(finalDetail);
            Assert.Equal("auto-approved", finalDetail!.Entries.First(entry => entry.Id == extraPendingEntryId).ResolutionStatus);
            Assert.Contains(
                finalDetail.Entries,
                entry => entry.Type == "tool" &&
                         entry.ToolName == "run_shell_command" &&
                         entry.ResolutionStatus == "executed-after-auto-approval");

            var otherSessionTurn = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Run the same shell family in another session.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "run_shell_command",
                    ToolArgumentsJson = """{"command":"dotnet help"}""",
                    ApproveToolExecution = false
                });

            Assert.Equal("approval-required", otherSessionTurn.ToolExecution.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_ApprovePendingToolAsync_AlwaysAllowUser_PersistsUserRuleAndSkipsMatchingFutureApproval()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-user-approval-rule-{Guid.NewGuid():N}");
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
                    "defaultMode": "default"
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var firstTurn = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a user-scoped shell approval.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "run_shell_command",
                    ToolArgumentsJson = """{"command":"dotnet help"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = firstTurn.Session.SessionId
                });
            Assert.NotNull(pendingDetail);
            var pendingEntry = Assert.Single(
                pendingDetail!.Entries,
                entry => entry.Type == "tool" && entry.Status == "approval-required");

            await sessionHost.ApprovePendingToolAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ApproveDesktopSessionToolRequest
                {
                    SessionId = firstTurn.Session.SessionId,
                    EntryId = pendingEntry.Id,
                    Decision = "always-allow-user"
                });

            var userSettingsPath = Path.Combine(homeRoot, ".qwen", "settings.json");
            Assert.Contains("Bash(dotnet *)", await File.ReadAllTextAsync(userSettingsPath), StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Bash(dotnet *)",
                await File.ReadAllTextAsync(Path.Combine(workspaceRoot, ".qwen", "settings.json")),
                StringComparison.Ordinal);

            var secondTurn = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Run the same shell family after a user-scoped allow.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "run_shell_command",
                    ToolArgumentsJson = """{"command":"dotnet help"}""",
                    ApproveToolExecution = false
                });

            Assert.NotEqual("approval-required", secondTurn.ToolExecution.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_ApprovePendingToolAsync_DenyWithFeedback_ResolvesPendingEntryAndContinuesTurn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-deny-pending-tool-{Guid.NewGuid():N}");
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
                    "ask": ["Write"]
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var startResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a pending edit for denial.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"should not write"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(pendingDetail);

            var pendingEntry = Assert.Single(
                pendingDetail!.Entries,
                entry => entry.Type == "tool" && entry.Status == "approval-required");

            var denialResult = await sessionHost.ApprovePendingToolAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new ApproveDesktopSessionToolRequest
                {
                    SessionId = startResult.Session.SessionId,
                    EntryId = pendingEntry.Id,
                    Decision = "deny",
                    Feedback = "Do not write the file. Inspect the current content first."
                });

            Assert.Equal("blocked", denialResult.ToolExecution.Status);
            Assert.Equal("deny", denialResult.ToolExecution.ApprovalState);
            Assert.False(File.Exists(targetFile));

            var finalDetail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = startResult.Session.SessionId
                });
            Assert.NotNull(finalDetail);
            Assert.Equal(0, finalDetail!.Summary.PendingApprovalCount);

            var resolvedPendingEntry = finalDetail.Entries.First(entry => entry.Id == pendingEntry.Id);
            Assert.Equal("denied", resolvedPendingEntry.ResolutionStatus);
            Assert.False(string.IsNullOrWhiteSpace(resolvedPendingEntry.ResolvedAt));

            var blockedExecutionEntry = finalDetail.Entries.Last(entry =>
                entry.Type == "tool" &&
                entry.ToolName == "write_file");
            Assert.Equal("blocked", blockedExecutionEntry.Status);
            Assert.Equal("blocked-by-user", blockedExecutionEntry.ResolutionStatus);
            Assert.Contains("denied approval", blockedExecutionEntry.Body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

}

