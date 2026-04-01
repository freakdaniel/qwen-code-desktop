using Microsoft.Extensions.Options;
using QwenCode.App.Compatibility;
using QwenCode.App.Desktop;
using QwenCode.App.Desktop.Diagnostics;
using QwenCode.App.Enums;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Permissions;
using QwenCode.App.Runtime;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;

namespace QwenCode.Tests;

public sealed class UnitTest1
{
    [Fact]
    public async Task SetLocaleAsync_FallsBackToKnownLanguageCode()
    {
        var service = CreateService();

        var state = await service.SetLocaleAsync("fr-CA");

        Assert.Equal("fr", state.CurrentLocale);
    }

    [Fact]
    public async Task GetBootstrapAsync_ReturnsConfiguredProductAndSources()
    {
        var expectedSources = new SourceMirrorPaths
        {
            WorkspaceRoot = "D:\\Projects\\qwen-code-desktop",
            QwenRoot = "D:\\Projects\\qwen-code-main",
            ClaudeRoot = "D:\\Projects\\claude-code-main",
            IpcReferenceRoot = "D:\\Projects\\HyPrism"
        };

        var service = CreateService(new DesktopShellOptions
        {
            ProductName = "Qwen Code Desktop",
            DefaultLocale = "ru",
            Sources = expectedSources
        });

        var payload = await service.GetBootstrapAsync();

        Assert.Equal("Qwen Code Desktop", payload.ProductName);
        Assert.Equal(DesktopMode.Code, payload.CurrentMode);
        Assert.Equal(expectedSources.QwenRoot, payload.Sources.QwenRoot);
        Assert.Contains(payload.Locales, locale => locale.Code == "ar");
        Assert.Contains(payload.SourceStatuses, status => status.Id == "qwen");
        Assert.Contains(payload.RuntimePortPlan, item => item.Id == "qwen-core-engine");
        Assert.Contains(payload.QwenCompatibility.SettingsLayers, layer => layer.Id == "project-settings");
        Assert.Equal("default", payload.QwenRuntime.ApprovalProfile.DefaultMode);
        Assert.True(payload.QwenTools.TotalCount >= 0);
        Assert.True(payload.QwenNativeHost.RegisteredCount >= 0);
    }

    [Fact]
    public void RuntimePortPlannerService_BuildPlan_UsesDetectedMarkers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-port-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var qwenRoot = Path.Combine(root, "qwen");
            var claudeRoot = Path.Combine(root, "claude");
            Directory.CreateDirectory(Path.Combine(qwenRoot, "packages", "core", "src", "tools"));
            Directory.CreateDirectory(Path.Combine(qwenRoot, ".qwen", "commands"));
            Directory.CreateDirectory(Path.Combine(qwenRoot, ".qwen", "skills"));
            Directory.CreateDirectory(Path.Combine(qwenRoot, "docs", "developers"));
            Directory.CreateDirectory(Path.Combine(qwenRoot, "docs", "users", "configuration"));
            Directory.CreateDirectory(Path.Combine(claudeRoot, "src", "bridge"));
            Directory.CreateDirectory(Path.Combine(claudeRoot, "src", "commands", "desktop"));
            Directory.CreateDirectory(Path.Combine(claudeRoot, "src", "commands", "session"));
            Directory.CreateDirectory(Path.Combine(claudeRoot, "src", "commands", "permissions"));
            Directory.CreateDirectory(Path.Combine(claudeRoot, "src", "commands", "plan"));

            File.WriteAllText(
                Path.Combine(qwenRoot, "package.json"),
                """
                {
                  "version": "0.14.0",
                  "workspaces": ["packages/*", "packages/channels/base", "packages/channels/telegram"]
                }
                """);
            File.WriteAllText(Path.Combine(qwenRoot, "packages", "core", "src", "tools", "tool-registry.ts"), "export {};");
            File.WriteAllText(Path.Combine(qwenRoot, "packages", "core", "src", "tools", "shell.ts"), "export {};");
            File.WriteAllText(Path.Combine(qwenRoot, "packages", "core", "src", "tools", "read-file.ts"), "export {};");
            File.WriteAllText(Path.Combine(qwenRoot, "docs", "developers", "architecture.md"), "# architecture");
            File.WriteAllText(Path.Combine(qwenRoot, "docs", "users", "configuration", "settings.md"), "# settings");

            File.WriteAllText(Path.Combine(claudeRoot, "src", "bridge", "types.ts"), "export type X = {};");
            File.WriteAllText(Path.Combine(claudeRoot, "src", "bridge", "sessionRunner.ts"), "export {};");
            File.WriteAllText(Path.Combine(claudeRoot, "src", "bridge", "codeSessionApi.ts"), "export {};");
            File.WriteAllText(Path.Combine(claudeRoot, "src", "bridge", "bridgePermissionCallbacks.ts"), "export {};");
            File.WriteAllText(Path.Combine(claudeRoot, "src", "commands", "desktop", "desktop.tsx"), "export const x = 1;");
            File.WriteAllText(Path.Combine(claudeRoot, "src", "commands", "statusline.tsx"), "export const x = 1;");

            var planner = new RuntimePortPlannerService();
            var plan = planner.BuildPlan(new SourceMirrorPaths
            {
                QwenRoot = qwenRoot,
                ClaudeRoot = claudeRoot
            });

            Assert.Contains(plan, item => item.Id == "qwen-core-engine" && item.Stage == "next");
            Assert.Contains(plan, item => item.Id == "qwen-tooling-host" && item.Stage == "next");
            Assert.Contains(plan, item => item.Id == "claude-workspace-ux" && item.Stage == "foundation");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenCompatibilityService_Inspect_ReturnsProjectAndUserLayers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-compat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "commands"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "skills"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen", "skills"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """{ "general": {}, "ui": {} }""");
            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """{ "privacy": {} }""");
            File.WriteAllText(Path.Combine(workspaceRoot, "QWEN.md"), "# project memory");

            var previousDefaults = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH");
            var previousSettings = Environment.GetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH");

            try
            {
                Environment.SetEnvironmentVariable(
                    "QWEN_CODE_SYSTEM_DEFAULTS_PATH",
                    Path.Combine(systemRoot, "system-defaults.json"));
                Environment.SetEnvironmentVariable(
                    "QWEN_CODE_SYSTEM_SETTINGS_PATH",
                    Path.Combine(systemRoot, "settings.json"));

                var service = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
                var snapshot = service.Inspect(new SourceMirrorPaths { WorkspaceRoot = workspaceRoot });

                Assert.Equal("QWEN.md", snapshot.DefaultContextFileName);
                Assert.Contains(snapshot.SettingsLayers, layer => layer.Id == "project-settings" && layer.Exists);
                Assert.Contains(snapshot.SettingsLayers, layer => layer.Id == "user-settings" && layer.Exists);
                Assert.Contains(snapshot.SurfaceDirectories, surface => surface.Id == "project-commands" && surface.Exists);
                Assert.Contains(snapshot.SurfaceDirectories, surface => surface.Id == "context-root" && surface.Exists);
            }
            finally
            {
                Environment.SetEnvironmentVariable("QWEN_CODE_SYSTEM_DEFAULTS_PATH", previousDefaults);
                Environment.SetEnvironmentVariable("QWEN_CODE_SYSTEM_SETTINGS_PATH", previousSettings);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenSlashCommandRuntime_TryResolve_LoadsProjectCommandAndRendersArgs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-command-runtime-{Guid.NewGuid():N}");
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

                Review PR {{args}} from {{cwd}} and summarize the risks.
                """
            );

            var runtime = new QwenSlashCommandRuntime(new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot)));
            var resolved = runtime.TryResolve(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                "/qc/code-review 123",
                workspaceRoot);

            Assert.NotNull(resolved);
            Assert.Equal("qc/code-review", resolved!.Name);
            Assert.Equal("project", resolved.Scope);
            Assert.Contains("123", resolved.ResolvedPrompt);
            Assert.Contains(workspaceRoot.Replace('\\', '/'), resolved.ResolvedPrompt);
            Assert.Contains("Code review a pull request", resolved.Description);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QwenCommandActionRuntime_TryInvokeAsync_ExecutesMemoryCommands()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-command-actions-{Guid.NewGuid():N}");
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
            File.WriteAllText(Path.Combine(homeRoot, ".qwen", "QWEN.md"), "# Global memory");

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var toolRegistry = new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService());
            var runtime = new QwenCommandActionRuntime(
                new QwenSlashCommandRuntime(compatibilityService),
                runtimeProfileService,
                compatibilityService,
                toolRegistry);

            var showResult = await runtime.TryInvokeAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                "/memory show",
                workspaceRoot);

            var addResult = await runtime.TryInvokeAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                "/memory add --project remember desktop port parity",
                workspaceRoot);

            var refreshResult = await runtime.TryInvokeAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                "/memory refresh",
                workspaceRoot);

            Assert.NotNull(showResult);
            Assert.Equal("completed", showResult!.Status);
            Assert.Contains("Project memory", showResult.Output);
            Assert.Contains("Global memory", showResult.Output);

            Assert.NotNull(addResult);
            Assert.Equal("completed", addResult!.Status);
            Assert.Contains("Saved memory", addResult.Output);
            Assert.Contains("remember desktop port parity", File.ReadAllText(Path.Combine(workspaceRoot, "QWEN.md")));

            Assert.NotNull(refreshResult);
            Assert.Equal("completed", refreshResult!.Status);
            Assert.Contains("Memory refreshed successfully", refreshResult.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QwenCommandActionRuntime_TryInvokeAsync_ExecutesContextCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-context-command-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "commands", "qc"));
            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen", "skills", "project-review"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(Path.Combine(workspaceRoot, "QWEN.md"), "# Project memory");
            File.WriteAllText(Path.Combine(homeRoot, ".qwen", "QWEN.md"), "# Global memory");
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "commands", "qc", "code-review.md"),
                """
                ---
                description: Code review a pull request
                ---
                """
            );
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "skills", "project-review", "SKILL.md"),
                """
                ---
                name: project-review
                description: Review project changes with local context
                ---
                """
            );

            var compatibilityService = new QwenCompatibilityService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var runtime = new QwenCommandActionRuntime(
                new QwenSlashCommandRuntime(compatibilityService),
                runtimeProfileService,
                compatibilityService,
                new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService()));

            var result = await runtime.TryInvokeAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                "/context detail",
                workspaceRoot);

            Assert.NotNull(result);
            Assert.Equal("completed", result!.Status);
            Assert.Equal("context", result.Command.Name);
            Assert.Contains("Workspace:", result.Output);
            Assert.Contains("Slash commands: 1", result.Output);
            Assert.Contains("Skills: 1", result.Output);
            Assert.Contains("qc/code-review", result.Output);
            Assert.Contains("project-review", result.Output);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenRuntimeProfileService_Inspect_ResolvesRuntimeOutputAndApprovalRules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(homeRoot, ".qwen"));
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(homeRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "allow": ["Bash(git *)"]
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "advanced": {
                    "runtimeOutputDir": ".qwen-runtime"
                  },
                  "permissions": {
                    "defaultMode": "auto-edit",
                    "confirmShellCommands": true,
                    "confirmFileEdits": false,
                    "ask": ["Edit"],
                    "deny": ["Read(.env)"]
                  },
                  "tools": {
                    "core": ["Read", "Write"]
                  },
                  "context": {
                    "fileName": ["TEAM.md", "QWEN.md"]
                  }
                }
                """);

            var service = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var profile = service.Inspect(new SourceMirrorPaths { WorkspaceRoot = workspaceRoot });

            Assert.Equal(Path.Combine(workspaceRoot, ".qwen-runtime"), profile.RuntimeBaseDirectory);
            Assert.Equal("project-settings", profile.RuntimeSource);
            Assert.Equal("auto-edit", profile.ApprovalProfile.DefaultMode);
            Assert.True(profile.ApprovalProfile.ConfirmShellCommands);
            Assert.False(profile.ApprovalProfile.ConfirmFileEdits);
            Assert.Contains("Bash(git *)", profile.ApprovalProfile.AllowRules);
            Assert.Contains("Read", profile.ApprovalProfile.AllowRules);
            Assert.Contains("Write", profile.ApprovalProfile.AllowRules);
            Assert.Contains("Edit", profile.ApprovalProfile.AskRules);
            Assert.Contains("Read(.env)", profile.ApprovalProfile.DenyRules);
            Assert.Equal(["TEAM.md", "QWEN.md"], profile.ContextFileNames);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
            var runtimeProfile = runtimeProfileService.Inspect(new SourceMirrorPaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var sessionFilePath = Path.Combine(runtimeProfile.ChatsDirectory, "12345678-1234-1234-1234-1234567890ab.jsonl");
            File.WriteAllLines(
                sessionFilePath,
                [
                    """
                    {"uuid":"u-1","parentUuid":null,"sessionId":"12345678-1234-1234-1234-1234567890ab","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"D:\\Projects\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Audit the renderer parity and plan the next native host step."}]}}
                    """,
                    """
                    {"uuid":"u-2","parentUuid":"u-1","sessionId":"12345678-1234-1234-1234-1234567890ab","timestamp":"2026-04-01T12:01:00Z","type":"assistant","cwd":"D:\\Projects\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"model","parts":[{"text":"I'll inspect the current desktop host."}]}}
                    """
                ]);
            File.SetLastWriteTimeUtc(sessionFilePath, DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)));

            var catalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessions = catalog.ListSessions(new SourceMirrorPaths { WorkspaceRoot = workspaceRoot });
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
            var profile = runtimeProfileService.Inspect(new SourceMirrorPaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(profile.ChatsDirectory);

            var sessionFilePath = Path.Combine(profile.ChatsDirectory, "detail-session.jsonl");
            File.WriteAllLines(
                sessionFilePath,
                [
                    """
                    {"uuid":"u-1","parentUuid":null,"sessionId":"detail-session","timestamp":"2026-04-01T12:00:00Z","type":"user","cwd":"D:\\Projects\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"user","parts":[{"text":"Inspect transcript detail."}]}}
                    """,
                    """
                    {"uuid":"u-2","parentUuid":"u-1","sessionId":"detail-session","timestamp":"2026-04-01T12:00:10Z","type":"command","cwd":"D:\\Projects\\demo","version":"0.1.0","gitBranch":"main","commandName":"context","resolvedPrompt":"Show the current runtime context.","status":"completed","output":"Workspace: D:\\Projects\\demo"}
                    """,
                    """
                    {"uuid":"u-3","parentUuid":"u-2","sessionId":"detail-session","timestamp":"2026-04-01T12:00:20Z","type":"tool","cwd":"D:\\Projects\\demo","version":"0.1.0","gitBranch":"main","toolName":"read_file","status":"completed","output":"README contents"}
                    """,
                    """
                    {"uuid":"u-4","parentUuid":"u-3","sessionId":"detail-session","timestamp":"2026-04-01T12:00:30Z","type":"assistant","cwd":"D:\\Projects\\demo","version":"0.1.0","gitBranch":"main","message":{"role":"assistant","parts":[{"text":"Transcript detail is available."}]}}
                    """
                ]);

            var catalog = new DesktopSessionCatalogService(runtimeProfileService);
            var detail = catalog.GetSession(new SourceMirrorPaths { WorkspaceRoot = workspaceRoot }, "detail-session");

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
                    Assert.Equal("Workspace: D:\\Projects\\demo", entry.Body);
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
    public void QwenToolCatalogService_Inspect_ParsesToolsAndAppliesApprovalProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var qwenRoot = Path.Combine(root, "qwen");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(Path.Combine(qwenRoot, "packages", "core", "src", "tools", "web-search"));
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
            File.WriteAllText(
                Path.Combine(qwenRoot, "packages", "core", "src", "tools", "tool-names.ts"),
                """
                export const ToolNames = {
                  READ_FILE: 'read_file',
                  EDIT: 'edit',
                  SHELL: 'run_shell_command'
                } as const;

                export const ToolDisplayNames = {
                  READ_FILE: 'ReadFile',
                  EDIT: 'Edit',
                  SHELL: 'Shell'
                } as const;
                """);

            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var catalog = new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService());
            var snapshot = catalog.Inspect(new SourceMirrorPaths
            {
                WorkspaceRoot = workspaceRoot,
                QwenRoot = qwenRoot
            });

            Assert.Equal("source-assisted", snapshot.SourceMode);
            Assert.Equal(3, snapshot.TotalCount);
            Assert.Contains(snapshot.Tools, tool => tool.Name == "read_file" && tool.ApprovalState == "allow");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "edit" && tool.ApprovalState == "ask");
            Assert.Contains(snapshot.Tools, tool => tool.Name == "run_shell_command" && tool.ApprovalState == "deny");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenApprovalPolicyService_Evaluate_UsesQwenStyleSpecifiersAndMetaCategories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-approvals-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectRoot = Path.Combine(root, "workspace");
            var docsRoot = Path.Combine(projectRoot, "docs");
            var srcRoot = Path.Combine(projectRoot, "src");
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);

            var service = new QwenApprovalPolicyService();
            var profile = new QwenApprovalProfile
            {
                DefaultMode = "default",
                AllowRules = ["Bash(git *)", "Read(./docs/**)"],
                AskRules = ["Edit(/src/**)"],
                DenyRules = ["WebFetch(domain:example.com)"]
            };

            var shellDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "git status"
                },
                profile);

            var readDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "grep_search",
                    Kind = "read",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    FilePath = Path.Combine(docsRoot, "guide.md")
                },
                profile);

            var editDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "write_file",
                    Kind = "modify",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    FilePath = Path.Combine(srcRoot, "Program.cs")
                },
                profile);

            var webDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "web_fetch",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Domain = "api.example.com"
                },
                profile);

            Assert.Equal("allow", shellDecision.State);
            Assert.Contains("Bash(git *)", shellDecision.Reason);
            Assert.Equal("allow", readDecision.State);
            Assert.Contains("Read(./docs/**)", readDecision.Reason);
            Assert.Equal("ask", editDecision.State);
            Assert.Contains("Edit(/src/**)", editDecision.Reason);
            Assert.Equal("deny", webDecision.State);
            Assert.Contains("WebFetch(domain:example.com)", webDecision.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QwenApprovalPolicyService_Evaluate_AppliesVirtualShellOperations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-virtual-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectRoot = Path.Combine(root, "workspace");
            var docsRoot = Path.Combine(projectRoot, "docs");
            var srcRoot = Path.Combine(projectRoot, "src");
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);

            var service = new QwenApprovalPolicyService();
            var profile = new QwenApprovalProfile
            {
                DefaultMode = "plan",
                AllowRules = ["Read(./docs/**)"],
                AskRules = ["Edit(/src/**)"],
                DenyRules = ["Read(.env)"]
            };

            var readDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "more docs\\guide.md"
                },
                profile);

            var editDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "echo hello > src/output.txt"
                },
                profile);

            var denyDecision = service.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = "run_shell_command",
                    Kind = "execute",
                    ProjectRoot = projectRoot,
                    WorkingDirectory = projectRoot,
                    Command = "cat .env"
                },
                profile);

            Assert.Equal("allow", readDecision.State);
            Assert.Contains("shell semantics", readDecision.Reason);
            Assert.Equal("ask", editDecision.State);
            Assert.Contains("Edit(/src/**)", editDecision.Reason);
            Assert.Equal("deny", denyDecision.State);
            Assert.Contains("Read(.env)", denyDecision.Reason);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QwenNativeToolHostService_ExecuteAsync_ReadsAndWritesWithApprovalGate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-native-{Guid.NewGuid():N}");
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
                    "ask": ["WriteFile"]
                  }
                }
                """);

            var sourcePaths = new SourceMirrorPaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService());

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            File.WriteAllText(targetFile, "alpha\nbeta\ngamma");

            var readResult = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "read_file",
                ArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","offset":1,"limit":1}"""
            });

            Assert.Equal("completed", readResult.Status);
            Assert.Contains("beta", readResult.Output);

            var gatedWrite = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "write_file",
                ArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"replaced"}"""
            });

            Assert.Equal("approval-required", gatedWrite.Status);

            var approvedWrite = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "write_file",
                ApproveExecution = true,
                ArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"replaced"}"""
            });

            Assert.Equal("completed", approvedWrite.Status);
            Assert.Equal("replaced", File.ReadAllText(targetFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QwenNativeToolHostService_ExecuteAsync_AppliesSpecifierScopedRules()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-native-scoped-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var docsRoot = Path.Combine(workspaceRoot, "docs");
            var srcRoot = Path.Combine(workspaceRoot, "src");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "plan",
                    "allow": ["Read(./docs/**)", "Bash(git *)"],
                    "ask": ["Edit(/src/**)"],
                    "deny": ["Read(.env)"]
                  }
                }
                """);

            var sourcePaths = new SourceMirrorPaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService());

            var docsFile = Path.Combine(docsRoot, "guide.md");
            var srcFile = Path.Combine(srcRoot, "Program.cs");
            var envFile = Path.Combine(workspaceRoot, ".env");
            File.WriteAllText(docsFile, "docs-content");
            File.WriteAllText(srcFile, "class Program {}");
            File.WriteAllText(envFile, "SECRET=1");

            var allowedRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "read_file",
                ArgumentsJson = $$"""{"file_path":"{{docsFile.Replace("\\", "\\\\")}}"}"""
            });

            var deniedRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "read_file",
                ArgumentsJson = $$"""{"file_path":"{{envFile.Replace("\\", "\\\\")}}"}"""
            });

            var gatedEdit = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "write_file",
                ArgumentsJson = $$"""{"file_path":"{{srcFile.Replace("\\", "\\\\")}}","content":"updated"}"""
            });

            var allowedShell = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"git --version"}""",
                ApproveExecution = false
            });

            Assert.Equal("completed", allowedRead.Status);
            Assert.Contains("docs-content", allowedRead.Output);
            Assert.Equal("blocked", deniedRead.Status);
            Assert.Contains("Read(.env)", deniedRead.ErrorMessage);
            Assert.Equal("approval-required", gatedEdit.Status);
            Assert.Contains("Edit(/src/**)", gatedEdit.ErrorMessage);
            Assert.Equal("completed", allowedShell.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task QwenNativeToolHostService_ExecuteAsync_AppliesVirtualShellPermissionSemantics()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-shell-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var workspaceRoot = Path.Combine(root, "workspace");
            var homeRoot = Path.Combine(root, "home");
            var systemRoot = Path.Combine(root, "system");
            var docsRoot = Path.Combine(workspaceRoot, "docs");
            var srcRoot = Path.Combine(workspaceRoot, "src");

            Directory.CreateDirectory(Path.Combine(workspaceRoot, ".qwen"));
            Directory.CreateDirectory(docsRoot);
            Directory.CreateDirectory(srcRoot);
            Directory.CreateDirectory(homeRoot);
            Directory.CreateDirectory(systemRoot);

            var docsFile = Path.Combine(docsRoot, "guide.txt");
            var srcFile = Path.Combine(srcRoot, "output.txt");
            var envFile = Path.Combine(workspaceRoot, ".env");
            File.WriteAllText(docsFile, "docs through shell");
            File.WriteAllText(envFile, "SECRET=1");

            File.WriteAllText(
                Path.Combine(workspaceRoot, ".qwen", "settings.json"),
                """
                {
                  "permissions": {
                    "defaultMode": "plan",
                    "allow": ["Read(./docs/**)"],
                    "ask": ["Edit(/src/**)"],
                    "deny": ["Read(.env)"]
                  }
                }
                """);

            var sourcePaths = new SourceMirrorPaths { WorkspaceRoot = workspaceRoot };
            var runtimeProfileService = new QwenRuntimeProfileService(new FakeDesktopEnvironmentPaths(homeRoot, systemRoot));
            var host = new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService());

            var allowedShellRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"more docs\\guide.txt"}"""
            });

            var gatedShellWrite = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"echo hello > src/output.txt"}"""
            });

            var deniedShellRead = await host.ExecuteAsync(sourcePaths, new ExecuteNativeToolRequest
            {
                ToolName = "run_shell_command",
                ArgumentsJson = """{"command":"cat .env"}"""
            });

            Assert.Equal("completed", allowedShellRead.Status);
            Assert.Contains("docs through shell", allowedShellRead.Output);
            Assert.Equal("approval-required", gatedShellWrite.Status);
            Assert.Contains("Edit(/src/**)", gatedShellWrite.ErrorMessage);
            Assert.Equal("blocked", deniedShellRead.Status);
            Assert.Contains("Read(.env)", deniedShellRead.ErrorMessage);
            Assert.False(File.Exists(srcFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService())),
                new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService()),
                new DesktopSessionCatalogService(runtimeProfileService));

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var result = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
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
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService())),
                new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService()),
                new DesktopSessionCatalogService(runtimeProfileService));

            var result = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
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
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService())),
                new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService()),
                new DesktopSessionCatalogService(runtimeProfileService));

            var result = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
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
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService())),
                new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService()),
                new DesktopSessionCatalogService(runtimeProfileService));

            var result = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
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
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService())),
                new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService()),
                sessionCatalog);

            var firstResult = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Create the initial session transcript.",
                    WorkingDirectory = workspaceRoot
                });

            var secondResult = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
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
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                firstResult.Session.SessionId);
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
            var sessionCatalog = new DesktopSessionCatalogService(runtimeProfileService);
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService())),
                new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService()),
                sessionCatalog);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var result = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Try an edit without pre-approval.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"blocked write"}""",
                    ApproveToolExecution = false
                });

            var detail = sessionCatalog.GetSession(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                result.Session.SessionId);

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
            var sessionHost = new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(compatibilityService),
                    runtimeProfileService,
                    compatibilityService,
                    new QwenToolCatalogService(runtimeProfileService, new QwenApprovalPolicyService())),
                new QwenNativeToolHostService(runtimeProfileService, new QwenApprovalPolicyService()),
                sessionCatalog);

            var targetFile = Path.Combine(workspaceRoot, "notes.txt");
            var startResult = await sessionHost.StartTurnAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                new StartDesktopSessionTurnRequest
                {
                    Prompt = "Queue a pending edit for approval.",
                    WorkingDirectory = workspaceRoot,
                    ToolName = "write_file",
                    ToolArgumentsJson = $$"""{"file_path":"{{targetFile.Replace("\\", "\\\\")}}","content":"approved write"}""",
                    ApproveToolExecution = false
                });

            var pendingDetail = sessionCatalog.GetSession(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                startResult.Session.SessionId);
            Assert.NotNull(pendingDetail);
            var pendingToolEntries = pendingDetail!.Entries.Where(entry => entry.Type == "tool").ToArray();
            var pendingToolEntry = Assert.Single(pendingToolEntries);

            var approvalResult = await sessionHost.ApprovePendingToolAsync(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                new ApproveDesktopSessionToolRequest
                {
                    SessionId = startResult.Session.SessionId,
                    EntryId = pendingToolEntry.Id
                });

            Assert.Equal("completed", approvalResult.ToolExecution.Status);
            Assert.Equal("write_file", approvalResult.ToolExecution.ToolName);
            Assert.Equal("approved write", File.ReadAllText(targetFile));

            var finalDetail = sessionCatalog.GetSession(
                new SourceMirrorPaths { WorkspaceRoot = workspaceRoot },
                startResult.Session.SessionId);
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

    private static DesktopAppService CreateService(DesktopShellOptions? options = null)
    {
        var environmentPaths = new FakeDesktopEnvironmentPaths(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
        var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
        var approvalPolicyService = new QwenApprovalPolicyService();

        return new DesktopAppService(
            Options.Create(options ?? new DesktopShellOptions()),
            new SourceMirrorInspectorService(),
            new RuntimePortPlannerService(),
            new DesktopSettingsResolver(
                new QwenCompatibilityService(environmentPaths),
                runtimeProfileService),
            new QwenToolCatalogService(runtimeProfileService, approvalPolicyService),
            new QwenNativeToolHostService(runtimeProfileService, approvalPolicyService),
            new DesktopSessionCatalogService(runtimeProfileService),
            new DesktopSessionHostService(
                runtimeProfileService,
                new QwenCommandActionRuntime(
                    new QwenSlashCommandRuntime(new QwenCompatibilityService(environmentPaths)),
                    runtimeProfileService,
                    new QwenCompatibilityService(environmentPaths),
                    new QwenToolCatalogService(runtimeProfileService, approvalPolicyService)),
                new QwenNativeToolHostService(runtimeProfileService, approvalPolicyService),
                new DesktopSessionCatalogService(runtimeProfileService)));
    }

    private sealed class FakeDesktopEnvironmentPaths(string homeDirectory, string? programDataDirectory)
        : IDesktopEnvironmentPaths
    {
        public string HomeDirectory { get; } = homeDirectory;

        public string? ProgramDataDirectory { get; } = programDataDirectory;
    }
}
