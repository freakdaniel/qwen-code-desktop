using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Enums;
using QwenCode.App.Models;
using QwenCode.App.Compatibility;
using QwenCode.App.Runtime;
using QwenCode.App.Tools;

namespace QwenCode.App.Sessions;

public sealed class DesktopSessionHostService(
    QwenRuntimeProfileService runtimeProfileService,
    ICommandActionRuntime commandActionRuntime,
    IToolExecutor nativeToolHostService,
    ITranscriptStore sessionCatalogService) : ISessionHost
{
    public async Task<DesktopSessionTurnResult> StartTurnAsync(
        SourceMirrorPaths paths,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Prompt is required to start a desktop session turn.");
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var workingDirectory = ResolveWorkingDirectory(runtimeProfile.ProjectRoot, request.WorkingDirectory);
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString() : request.SessionId;
        var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl");
        var createdNewSession = !File.Exists(transcriptPath);
        var gitBranch = TryReadGitBranch(workingDirectory);
        var parentUuid = TryReadLastEntryUuid(transcriptPath);
        var timestampUtc = DateTime.UtcNow;
        var commandInvocation = await commandActionRuntime.TryInvokeAsync(paths, request.Prompt, workingDirectory, cancellationToken);
        var resolvedCommand = commandInvocation?.Command;

        Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

        var userUuid = Guid.NewGuid().ToString();
        await AppendEntryAsync(
            transcriptPath,
            new
            {
                uuid = userUuid,
                parentUuid,
                sessionId,
                timestamp = timestampUtc,
                type = "user",
                cwd = workingDirectory,
                version = "0.1.0",
                gitBranch,
                mode = DesktopMode.Code.ToString().ToLowerInvariant(),
                message = new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = request.Prompt
                        }
                    }
                }
            },
            cancellationToken);

        parentUuid = userUuid;
        if (commandInvocation is not null)
        {
            var commandUuid = Guid.NewGuid().ToString();
            await AppendEntryAsync(
                transcriptPath,
                new
                {
                    uuid = commandUuid,
                    parentUuid,
                    sessionId,
                    timestamp = DateTime.UtcNow,
                    type = "command",
                    cwd = workingDirectory,
                    version = "0.1.0",
                    gitBranch,
                    commandName = commandInvocation.Command.Name,
                    scope = commandInvocation.Command.Scope,
                    sourcePath = commandInvocation.Command.SourcePath,
                    args = commandInvocation.Command.Arguments,
                    resolvedPrompt = commandInvocation.Command.ResolvedPrompt,
                    status = commandInvocation.Status,
                    output = commandInvocation.Output,
                    errorMessage = commandInvocation.ErrorMessage
                },
                cancellationToken);

            parentUuid = commandUuid;
        }

        var toolExecution = CreateNoToolExecutionResult(workingDirectory);
        if (!string.IsNullOrWhiteSpace(request.ToolName))
        {
            toolExecution = await nativeToolHostService.ExecuteAsync(
                paths,
                new ExecuteNativeToolRequest
                {
                    ToolName = request.ToolName,
                    ArgumentsJson = string.IsNullOrWhiteSpace(request.ToolArgumentsJson) ? "{}" : request.ToolArgumentsJson,
                    ApproveExecution = request.ApproveToolExecution
                },
                cancellationToken);

            var toolUuid = Guid.NewGuid().ToString();
            await AppendEntryAsync(
                transcriptPath,
                new
                {
                    uuid = toolUuid,
                    parentUuid,
                    sessionId,
                    timestamp = DateTime.UtcNow,
                    type = "tool",
                    cwd = toolExecution.WorkingDirectory,
                    version = "0.1.0",
                    gitBranch,
                    toolName = toolExecution.ToolName,
                    args = string.IsNullOrWhiteSpace(request.ToolArgumentsJson) ? "{}" : request.ToolArgumentsJson,
                    approvalState = toolExecution.ApprovalState,
                    status = toolExecution.Status,
                    output = toolExecution.Output,
                    errorMessage = toolExecution.ErrorMessage,
                    exitCode = toolExecution.ExitCode,
                    changedFiles = toolExecution.ChangedFiles
                },
                cancellationToken);

            parentUuid = toolUuid;
        }

        var assistantSummary = BuildAssistantSummary(request, commandInvocation, toolExecution);
        await AppendEntryAsync(
            transcriptPath,
            new
            {
                uuid = Guid.NewGuid().ToString(),
                parentUuid,
                sessionId,
                timestamp = DateTime.UtcNow,
                type = "assistant",
                cwd = workingDirectory,
                version = "0.1.0",
                gitBranch,
                mode = DesktopMode.Code.ToString().ToLowerInvariant(),
                message = new
                {
                    role = "assistant",
                    parts = new[]
                    {
                        new
                        {
                            text = assistantSummary
                        }
                    }
                }
            },
            cancellationToken);

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, sessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(sessionId, transcriptPath, workingDirectory, gitBranch, request.Prompt);

        return new DesktopSessionTurnResult
        {
            Session = session,
            AssistantSummary = assistantSummary,
            CreatedNewSession = createdNewSession,
            ToolExecution = toolExecution,
            ResolvedCommand = resolvedCommand
        };
    }

    public async Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        SourceMirrorPaths paths,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to approve a pending tool.");
        }

        var detail = sessionCatalogService.GetSession(paths, request.SessionId)
            ?? throw new InvalidOperationException("Session transcript was not found.");
        var pendingTool = detail.Entries
            .Where(static entry =>
                entry.Type == "tool" &&
                string.Equals(entry.Status, "approval-required", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(entry.ResolutionStatus))
            .Where(entry => string.IsNullOrWhiteSpace(request.EntryId) || string.Equals(entry.Id, request.EntryId, StringComparison.Ordinal))
            .LastOrDefault()
            ?? throw new InvalidOperationException("No pending tool approval was found for this session.");

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var execution = await nativeToolHostService.ExecuteAsync(
            paths,
            new ExecuteNativeToolRequest
            {
                ToolName = pendingTool.ToolName,
                ArgumentsJson = string.IsNullOrWhiteSpace(pendingTool.Arguments) ? "{}" : pendingTool.Arguments,
                ApproveExecution = true
            },
            cancellationToken);

        var resolutionTimestamp = DateTime.UtcNow;
        await MarkToolEntryResolvedAsync(
            detail.TranscriptPath,
            pendingTool.Id,
            "approved",
            resolutionTimestamp,
            cancellationToken);

        var parentUuid = TryReadLastEntryUuid(detail.TranscriptPath);
        var gitBranch = pendingTool.GitBranch;
        var workingDirectory = string.IsNullOrWhiteSpace(pendingTool.WorkingDirectory)
            ? runtimeProfile.ProjectRoot
            : pendingTool.WorkingDirectory;

        var toolUuid = Guid.NewGuid().ToString();
        await AppendEntryAsync(
            detail.TranscriptPath,
            new
            {
                uuid = toolUuid,
                parentUuid,
                sessionId = request.SessionId,
                timestamp = resolutionTimestamp,
                type = "tool",
                cwd = execution.WorkingDirectory,
                version = "0.1.0",
                gitBranch,
                toolName = execution.ToolName,
                args = string.IsNullOrWhiteSpace(pendingTool.Arguments) ? "{}" : pendingTool.Arguments,
                approvalState = execution.ApprovalState,
                status = execution.Status,
                output = execution.Output,
                errorMessage = execution.ErrorMessage,
                exitCode = execution.ExitCode,
                changedFiles = execution.ChangedFiles,
                resolutionStatus = "executed-after-approval",
                sourcePath = pendingTool.SourcePath,
                scope = pendingTool.Scope
            },
            cancellationToken);

        var assistantSummary = execution.Status switch
        {
            "completed" => $"Approved native tool '{execution.ToolName}' and executed it inside the .NET host.",
            "blocked" => $"Approved native tool '{execution.ToolName}', but the execution is now blocked by qwen-compatible approval policy.",
            "error" => $"Approved native tool '{execution.ToolName}', but execution failed: {execution.ErrorMessage}",
            _ => $"Approved native tool '{pendingTool.ToolName}' for execution."
        };

        await AppendEntryAsync(
            detail.TranscriptPath,
            new
            {
                uuid = Guid.NewGuid().ToString(),
                parentUuid = toolUuid,
                sessionId = request.SessionId,
                timestamp = DateTime.UtcNow,
                type = "assistant",
                cwd = workingDirectory,
                version = "0.1.0",
                gitBranch,
                mode = DesktopMode.Code.ToString().ToLowerInvariant(),
                message = new
                {
                    role = "assistant",
                    parts = new[]
                    {
                        new
                        {
                            text = assistantSummary
                        }
                    }
                }
            },
            cancellationToken);

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, request.SessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(request.SessionId, detail.TranscriptPath, workingDirectory, gitBranch, pendingTool.Title);

        return new DesktopSessionTurnResult
        {
            Session = session,
            AssistantSummary = assistantSummary,
            CreatedNewSession = false,
            ToolExecution = execution,
            ResolvedCommand = null
        };
    }

    private static SessionPreview BuildFallbackSession(
        string sessionId,
        string transcriptPath,
        string workingDirectory,
        string gitBranch,
        string prompt) =>
        new()
        {
            SessionId = sessionId,
            Title = prompt.Length > 140 ? $"{prompt[..140]}..." : prompt,
            LastActivity = "Updated just now",
            Category = string.IsNullOrWhiteSpace(gitBranch) ? "session" : gitBranch,
            Mode = DesktopMode.Code,
            Status = "resume-ready",
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            MessageCount = 2,
            TranscriptPath = transcriptPath
        };

    private static string ResolveWorkingDirectory(string workspaceRoot, string requestedWorkingDirectory)
    {
        var resolved = string.IsNullOrWhiteSpace(requestedWorkingDirectory)
            ? workspaceRoot
            : Path.IsPathRooted(requestedWorkingDirectory)
                ? Path.GetFullPath(requestedWorkingDirectory)
                : Path.GetFullPath(Path.Combine(workspaceRoot, requestedWorkingDirectory));

        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        if (!resolved.StartsWith(
                fullWorkspaceRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Session working directory must stay inside the workspace root.");
        }

        Directory.CreateDirectory(resolved);
        return resolved;
    }

    private static async Task AppendEntryAsync(
        string transcriptPath,
        object payload,
        CancellationToken cancellationToken)
    {
        var line = JsonSerializer.Serialize(payload);
        await File.AppendAllTextAsync(
            transcriptPath,
            line + Environment.NewLine,
            cancellationToken);
    }

    private static async Task MarkToolEntryResolvedAsync(
        string transcriptPath,
        string entryId,
        string resolutionStatus,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(transcriptPath, cancellationToken);
        var updated = false;

        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            JsonObject? node;
            try
            {
                node = JsonNode.Parse(lines[index])?.AsObject();
            }
            catch
            {
                continue;
            }

            if (node is null ||
                !string.Equals(node["uuid"]?.GetValue<string>(), entryId, StringComparison.Ordinal))
            {
                continue;
            }

            node["resolutionStatus"] = resolutionStatus;
            node["resolvedAt"] = resolvedAtUtc;
            lines[index] = node.ToJsonString();
            updated = true;
            break;
        }

        if (updated)
        {
            await File.WriteAllLinesAsync(transcriptPath, lines, cancellationToken);
        }
    }

    private static string? TryReadLastEntryUuid(string transcriptPath)
    {
        if (!File.Exists(transcriptPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(transcriptPath).Reverse())
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("uuid", out var uuidProperty) &&
                    uuidProperty.ValueKind == JsonValueKind.String)
                {
                    return uuidProperty.GetString();
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static string TryReadGitBranch(string workingDirectory)
    {
        var currentDirectory = new DirectoryInfo(workingDirectory);
        while (currentDirectory is not null)
        {
            var gitDirectory = Path.Combine(currentDirectory.FullName, ".git");
            if (Directory.Exists(gitDirectory))
            {
                var headPath = Path.Combine(gitDirectory, "HEAD");
                if (!File.Exists(headPath))
                {
                    return string.Empty;
                }

                var head = File.ReadAllText(headPath).Trim();
                const string prefix = "ref: refs/heads/";
                return head.StartsWith(prefix, StringComparison.Ordinal)
                    ? head[prefix.Length..]
                    : head;
            }

            currentDirectory = currentDirectory.Parent;
        }

        return string.Empty;
    }

    private static string BuildAssistantSummary(
        StartDesktopSessionTurnRequest request,
        CommandInvocationResult? commandInvocation,
        QwenNativeToolExecutionResult toolExecution)
    {
        if (commandInvocation is not null && commandInvocation.IsTerminal)
        {
            return commandInvocation.Status switch
            {
                "completed" => $"Built-in command '/{commandInvocation.Command.Name}' completed in the native .NET runtime.",
                "error" => $"Built-in command '/{commandInvocation.Command.Name}' failed: {commandInvocation.ErrorMessage}",
                _ => $"Built-in command '/{commandInvocation.Command.Name}' updated the desktop session."
            };
        }

        var resolvedCommand = commandInvocation?.Command;
        if (resolvedCommand is not null && toolExecution.Status == "not-requested")
        {
            return $"Slash command '/{resolvedCommand.Name}' resolved by the native .NET runtime.";
        }

        if (resolvedCommand is not null)
        {
            return toolExecution.Status switch
            {
                "completed" => $"Slash command '/{resolvedCommand.Name}' resolved and native tool '{toolExecution.ToolName}' completed inside the .NET host.",
                "approval-required" => $"Slash command '/{resolvedCommand.Name}' resolved and native tool '{toolExecution.ToolName}' is waiting for approval.",
                "blocked" => $"Slash command '/{resolvedCommand.Name}' resolved, but native tool '{toolExecution.ToolName}' was blocked by qwen-compatible approval policy.",
                "error" => $"Slash command '/{resolvedCommand.Name}' resolved, but native tool '{toolExecution.ToolName}' failed: {toolExecution.ErrorMessage}",
                _ => $"Slash command '/{resolvedCommand.Name}' updated the desktop session."
            };
        }

        if (toolExecution.Status == "not-requested")
        {
            return "Turn recorded in the native desktop session host.";
        }

        return toolExecution.Status switch
        {
            "completed" => $"Native tool '{toolExecution.ToolName}' completed inside the .NET host.",
            "approval-required" => $"Native tool '{toolExecution.ToolName}' is waiting for approval before execution.",
            "blocked" => $"Native tool '{toolExecution.ToolName}' was blocked by qwen-compatible approval policy.",
            "error" => $"Native tool '{toolExecution.ToolName}' failed: {toolExecution.ErrorMessage}",
            _ => $"Native tool '{request.ToolName}' updated the desktop session."
        };
    }

    private static QwenNativeToolExecutionResult CreateNoToolExecutionResult(string workingDirectory) =>
        new()
        {
            ToolName = string.Empty,
            Status = "not-requested",
            ApprovalState = "allow",
            WorkingDirectory = workingDirectory,
            Output = string.Empty,
            ErrorMessage = string.Empty,
            ExitCode = 0,
            ChangedFiles = []
        };
}
