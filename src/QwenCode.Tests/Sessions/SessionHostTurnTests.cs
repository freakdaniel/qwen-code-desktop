namespace QwenCode.Tests.Sessions;

public sealed class SessionHostTurnTests
{
    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_WritesTranscriptAndRunsNativeTool()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-hosted-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".git"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(Path.Combine(workspaceRoot, ".git", "HEAD"), "ref: refs/heads/main");
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "default",
                    "allow": ["Read"],
                    "ask": ["WriteFile"]
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Create a native desktop transcript entry and write notes.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"hello from native session host"}""",
                    ApproveToolExecution = true
                });

            Assert.True(result.CreatedNewSession);
            Assert.NotNull(result.ToolExecution);
            Assert.Equal("completed", result.ToolExecution!.Status);
            Assert.Equal("main", result.Session.GitBranch);
            Assert.Equal("resume-ready", result.Session.Status);
            Assert.Equal("hello from native session host", File.ReadAllText(targetFile));
            Assert.True(File.Exists(result.Session.TranscriptPath));

            var transcript = File.ReadAllLines(result.Session.TranscriptPath);
            Assert.Equal(3, transcript.Length);
            Assert.Contains("\"type\":\"user\"", transcript[0]);
            Assert.Contains("\"type\":\"tool\"", transcript[1]);
            Assert.Contains("\"type\":\"assistant\"", transcript[2]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_AllowsRuntimeTempWorkingDirectoryForProjectlessChats()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-projectless-session-{Guid.NewGuid():N}");
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
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            var projectlessDirectory = Path.Combine(runtimeProfile.RuntimeBaseDirectory, "tmp", "no-project");

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Continue without a project and just chat.",
                    WorkingDirectory = projectlessDirectory
                });

            Assert.True(result.CreatedNewSession);
            Assert.Equal(projectlessDirectory, result.Session.WorkingDirectory);
            Assert.True(Directory.Exists(projectlessDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_AllowsExistingExternalProjectDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-external-project-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var externalProjectRoot = Path.Combine(root, "external-project");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(externalProjectRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService);

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Work in the selected external project directory.",
                    WorkingDirectory = externalProjectRoot
                });

            Assert.True(result.CreatedNewSession);
            Assert.Equal(externalProjectRoot, result.Session.WorkingDirectory);
            Assert.True(File.Exists(result.Session.TranscriptPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_ResolvesSlashCommandIntoTranscript()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-command-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "commands", "qc"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "commands", "qc", "code-review.md"),
                """
                ---
                description: Code review a pull request
                ---

                Review PR {{args}} and report the main risks.
                """
            );

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService);

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "/qc/code-review 42",
                    WorkingDirectory = workspaceRoot
                });

            Assert.NotNull(result.ResolvedCommand);
            Assert.Equal("qc/code-review", result.ResolvedCommand!.Name);
            Assert.Contains("42", result.ResolvedCommand.ResolvedPrompt);
            Assert.Contains("/qc/code-review", result.AssistantSummary);
            Assert.True(File.Exists(result.Session.TranscriptPath));

            var transcript = File.ReadAllLines(result.Session.TranscriptPath);
            Assert.Equal(3, transcript.Length);
            Assert.Contains("\"type\":\"user\"", transcript[0]);
            Assert.Contains("\"type\":\"command\"", transcript[1]);
            Assert.Contains("\"type\":\"assistant\"", transcript[2]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_ExecutesBuiltInMemoryCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-memory-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService);

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "/memory add --project remember built-in command execution",
                    WorkingDirectory = workspaceRoot
                });

            Assert.NotNull(result.ResolvedCommand);
            Assert.Equal("memory/add/project", result.ResolvedCommand!.Name);
            Assert.Contains("Built-in command", result.AssistantSummary);
            Assert.True(File.Exists(Path.Combine(workspaceRoot, "QWEN.md")));
            Assert.Contains("remember built-in command execution", File.ReadAllText(Path.Combine(workspaceRoot, "QWEN.md")));

            var transcript = File.ReadAllLines(result.Session.TranscriptPath);
            Assert.Equal(3, transcript.Length);
            Assert.Contains("\"type\":\"command\"", transcript[1]);
            Assert.Contains("\"status\":\"completed\"", transcript[1]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_ExecutesBuiltInContextCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-context-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);
            File.WriteAllText(Path.Combine(workspaceRoot, "QWEN.md"), "# Project memory");

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService);

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "/context",
                    WorkingDirectory = workspaceRoot
                });

            Assert.NotNull(result.ResolvedCommand);
            Assert.Equal("context", result.ResolvedCommand!.Name);
            Assert.Contains("Built-in command", result.AssistantSummary);

            var transcript = File.ReadAllLines(result.Session.TranscriptPath);
            Assert.Equal(3, transcript.Length);
            Assert.Contains("\"type\":\"command\"", transcript[1]);
            Assert.Contains("\"status\":\"completed\"", transcript[1]);
            Assert.Contains("Workspace:", transcript[1]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_AppendsToExistingSessionTranscript()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-resume-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".git"));
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(Path.Combine(workspaceRoot, ".git", "HEAD"), "ref: refs/heads/main");

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var firstResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Create the initial session transcript.",
                    WorkingDirectory = workspaceRoot
                });

            var secondResult = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    SessionId = firstResult.Session.SessionId,
                    Prompt = "Append another turn to the same session.",
                    WorkingDirectory = workspaceRoot
                });

            Assert.False(secondResult.CreatedNewSession);
            Assert.Equal(firstResult.Session.SessionId, secondResult.Session.SessionId);
            Assert.Equal(firstResult.Session.TranscriptPath, secondResult.Session.TranscriptPath);

            var transcript = File.ReadAllLines(secondResult.Session.TranscriptPath);
            Assert.Equal(4, transcript.Length);
            Assert.Contains("Create the initial session transcript.", string.Join(Environment.NewLine, transcript));
            Assert.Contains("Append another turn to the same session.", string.Join(Environment.NewLine, transcript));

            var detail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = firstResult.Session.SessionId
                });
            Assert.NotNull(detail);
            Assert.Equal(4, detail!.EntryCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_RecordsToolApprovalStateInSessionDetail()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-tool-detail-{Guid.NewGuid():N}");
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
            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Try an edit without pre-approval.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"blocked write"}""",
                    ApproveToolExecution = false
                });

            var detail = sessionCatalog.GetSession(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new GetDesktopSessionRequest
                {
                    SessionId = result.Session.SessionId
                });

            Assert.NotNull(detail);
            Assert.Equal(1, detail!.Summary.PendingApprovalCount);
            var toolEntries = detail.Entries.Where(entry => entry.Type == "tool").ToArray();
            var toolEntry = Assert.Single(toolEntries);
            Assert.Equal("write_file", toolEntry.ToolName);
            Assert.Equal("approval-required", toolEntry.Status);
            Assert.Equal("ask", toolEntry.ApprovalState);
            Assert.Contains("blocked write", toolEntry.Arguments);
            Assert.Contains("Requires confirmation", toolEntry.Body);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_EmitsLifecycleEvents()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-events-{Guid.NewGuid():N}");
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

            var emittedEvents = new List<DesktopSessionEvent>();
            sessionHost.SessionEvent += (_, sessionEvent) => emittedEvents.Add(sessionEvent);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "/context detail",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"needs approval"}""",
                    ApproveToolExecution = false
                });

            Assert.Collection(
                emittedEvents.Select(item => item.Kind),
                kind => Assert.Equal(DesktopSessionEventKind.TurnStarted, kind),
                kind => Assert.Equal(DesktopSessionEventKind.CommandCompleted, kind),
                kind => Assert.Equal(DesktopSessionEventKind.ToolApprovalRequired, kind),
                kind => Assert.Equal(DesktopSessionEventKind.AssistantPreparingContext, kind),
                kind => Assert.Equal(DesktopSessionEventKind.AssistantGenerating, kind),
                kind => Assert.Equal(DesktopSessionEventKind.AssistantCompleted, kind),
                kind => Assert.Equal(DesktopSessionEventKind.TurnCompleted, kind));

            Assert.All(emittedEvents, item => Assert.Equal(result.Session.SessionId, item.SessionId));
            Assert.Contains(emittedEvents, item => item.CommandName == "context");
            Assert.Contains(emittedEvents, item => item.ToolName == "write_file");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_WritesChatRecordingMetadataSidecar()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-recording-{Guid.NewGuid():N}");
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
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService, sessionCatalog);

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Record richer session metadata for this transcript.",
                    WorkingDirectory = workspaceRoot
                });

            Assert.True(File.Exists(result.Session.MetadataPath));

            var metadata = JsonSerializer.Deserialize<SessionRecordingMetadata>(File.ReadAllText(result.Session.MetadataPath));
            Assert.NotNull(metadata);
            Assert.Equal(result.Session.SessionId, metadata!.SessionId);
            Assert.Equal(result.Session.TranscriptPath, metadata.TranscriptPath);
            Assert.Equal(workspaceRoot, metadata.WorkingDirectory);
            Assert.Equal("resume-ready", metadata.Status);
            Assert.Equal(2, metadata.MessageCount);
            Assert.Equal(2, metadata.EntryCount);
            Assert.False(string.IsNullOrWhiteSpace(metadata.LastCompletedUuid));
            Assert.Equal(metadata.LastUpdatedAt, result.Session.LastUpdatedAt);
            Assert.Equal(metadata.StartedAt, result.Session.StartedAt);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DesktopSessionHostService_StartTurnAsync_AppendsCompressionCheckpointForLongSessions()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-compression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(workspaceRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            await File.WriteAllTextAsync(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "checkpointing": true,
                  "chatCompression": {
                    "contextPercentageThreshold": 0.001
                  }
                }
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var sessionHost = CreateSessionHost(runtimeProfileService, compatibilityService);
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "preloaded.jsonl");
            using (var writer = new StreamWriter(transcriptPath))
            {
                for (var index = 0; index < 24; index++)
                {
                    var type = index % 2 == 0 ? "user" : "assistant";
                    var role = type == "user" ? "user" : "assistant";
                    writer.WriteLine(
                        JsonSerializer.Serialize(new
                        {
                            uuid = Guid.NewGuid().ToString(),
                            sessionId = "preloaded",
                            timestamp = DateTime.UtcNow.AddMinutes(-30 + index).ToString("O"),
                            type,
                            cwd = workspaceRoot,
                            version = "0.1.0",
                            gitBranch = "main",
                            message = new
                            {
                                role,
                                parts = new[]
                                {
                                    new { text = $"Historical message {index}" }
                                }
                            }
                        }));
                }
            }

            var result = await sessionHost.StartTurnAsync(
                new WorkspacePaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    SessionId = "preloaded",
                    Prompt = "Continue after a long transcript history.",
                    WorkingDirectory = workspaceRoot
                });

            var transcript = File.ReadAllLines(result.Session.TranscriptPath);
            Assert.Contains(transcript, line => line.Contains("\"status\":\"chat-compression\"", StringComparison.Ordinal));
            Assert.Contains(transcript, line => line.Contains("Compression checkpoint:", StringComparison.Ordinal));
            Assert.Contains(transcript, line => line.Contains("tokens", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


}

