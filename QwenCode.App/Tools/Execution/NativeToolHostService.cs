using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using QwenCode.App.Agents;
using QwenCode.App.Compatibility;
using QwenCode.App.Hooks;
using QwenCode.App.Infrastructure;
using QwenCode.App.Mcp;
using QwenCode.App.Models;
using QwenCode.App.Permissions;
using QwenCode.App.Runtime;

namespace QwenCode.App.Tools;

public sealed class NativeToolHostService(
    QwenRuntimeProfileService runtimeProfileService,
    IApprovalPolicyEngine approvalPolicyService,
    ICronScheduler? cronScheduler = null,
    IWebToolService? webToolService = null,
    IUserQuestionToolService? userQuestionToolService = null,
    IMcpToolRuntime? mcpToolRuntime = null,
    ILspToolService? lspToolService = null,
    ISkillToolService? skillToolService = null,
    ISubagentCoordinator? subagentCoordinator = null,
    IHookLifecycleService? hookLifecycleService = null,
    IShellExecutionService? shellExecutionService = null) : IToolExecutor
{
    private static readonly string[] IgnoredDirectories = [".git", "node_modules", "bin", "obj", ".electron", "dist"];
    private readonly ISubagentCoordinator agents = subagentCoordinator ?? CreateFallbackSubagentCoordinator(runtimeProfileService, approvalPolicyService);
    private readonly ICronScheduler cron = cronScheduler ?? new InMemoryCronScheduler();
    private readonly IShellExecutionService shell = shellExecutionService ?? new ShellExecutionService();
    private readonly IWebToolService webTools = webToolService ?? new WebToolService(new DefaultDesktopEnvironmentPaths());
    private readonly IUserQuestionToolService userQuestions = userQuestionToolService ?? new UserQuestionToolService();
    private readonly IMcpToolRuntime? mcpTools = mcpToolRuntime;
    private readonly ILspToolService lspTools = lspToolService ?? new RoslynLspToolService();
    private readonly ISkillToolService skills = skillToolService ?? new SkillToolService(new QwenCompatibilityService(new DefaultDesktopEnvironmentPaths()));
    private readonly IHookLifecycleService? hooks = hookLifecycleService;

    public NativeToolHostSnapshot Inspect(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var registrations = ToolContractCatalog.Implemented
            .Select(tool =>
            {
                var approval = approvalPolicyService.Evaluate(
                    new ApprovalCheckContext
                    {
                        ToolName = tool.Name,
                        Kind = tool.Kind,
                        ProjectRoot = runtimeProfile.ProjectRoot,
                        WorkingDirectory = runtimeProfile.ProjectRoot
                    },
                    runtimeProfile.ApprovalProfile);
                return new NativeToolRegistration
                {
                    Name = tool.Name,
                    DisplayName = tool.DisplayName,
                    Kind = tool.Kind,
                    IsImplemented = true,
                    ApprovalState = approval.State,
                    ApprovalReason = approval.Reason
                };
            })
            .OrderBy(static tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new NativeToolHostSnapshot
        {
            RegisteredCount = registrations.Length,
            ImplementedCount = registrations.Count(static tool => tool.IsImplemented),
            ReadyCount = registrations.Count(static tool => tool.ApprovalState == "allow"),
            ApprovalRequiredCount = registrations.Count(static tool => tool.ApprovalState == "ask"),
            Tools = registrations
        };
    }

    public async Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        ExecuteNativeToolRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        if (!ToolContractCatalog.ByName.TryGetValue(request.ToolName, out var tool) || !tool.IsImplemented)
        {
            return Error(request.ToolName, "Tool is not implemented by the native .NET host yet.", paths.WorkspaceRoot);
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson);
        }
        catch (Exception exception)
        {
            return Error(request.ToolName, $"Failed to parse tool arguments: {exception.Message}", runtimeProfile.ProjectRoot);
        }

        using (document)
        {
            var approvalContext = BuildApprovalContext(request.ToolName, tool.Kind, runtimeProfile, document.RootElement);
            var approval = approvalPolicyService.Evaluate(approvalContext, runtimeProfile.ApprovalProfile);
            if (string.Equals(request.ToolName, "mcp-tool", StringComparison.OrdinalIgnoreCase))
            {
                var mcpApproval = await EvaluateMcpToolApprovalAsync(paths, runtimeProfile, document.RootElement, cancellationToken);
                if (mcpApproval.ErrorResult is not null)
                {
                    return mcpApproval.ErrorResult;
                }

                approval = mcpApproval.Decision!;
            }

            if (approval.State == "deny")
            {
                return new NativeToolExecutionResult
                {
                    ToolName = request.ToolName,
                    Status = "blocked",
                    ApprovalState = approval.State,
                    WorkingDirectory = approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot,
                    ErrorMessage = approval.Reason,
                    ChangedFiles = []
                };
            }

            if (string.Equals(request.ToolName, "ask_user_question", StringComparison.OrdinalIgnoreCase))
            {
                var questionResult = ExecuteAskUserQuestion(runtimeProfile, approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot, document.RootElement, approval.State);
                return await ApplyPostToolHooksAsync(runtimeProfile, request, document.RootElement, questionResult, cancellationToken);
            }

            if (approval.State == "ask" && !request.ApproveExecution)
            {
                var approvalRequiredResult = new NativeToolExecutionResult
                {
                    ToolName = request.ToolName,
                    Status = "approval-required",
                    ApprovalState = approval.State,
                    WorkingDirectory = approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot,
                    ErrorMessage = approval.Reason,
                    ChangedFiles = []
                };
                return await ApplyPermissionRequestHooksAsync(runtimeProfile, request, document.RootElement, approvalRequiredResult, cancellationToken);
            }

            var preToolHook = await ExecuteHookAsync(
                runtimeProfile,
                request,
                document.RootElement,
                HookEventName.PreToolUse,
                approval.State,
                workingDirectory: approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot,
                cancellationToken: cancellationToken);
            if (preToolHook.IsBlocked)
            {
                return CreateHookBlockedToolResult(
                    request.ToolName,
                    approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot,
                    approval.State,
                    preToolHook.BlockReason);
            }

            var result = await ExecuteToolCoreAsync(paths, request, runtimeProfile, document.RootElement, approval.State, eventSink, cancellationToken);
            return await ApplyPostToolHooksAsync(runtimeProfile, request, document.RootElement, result, cancellationToken);
        }
    }

    private Task<NativeToolExecutionResult> ExecuteToolCoreAsync(
        WorkspacePaths paths,
        ExecuteNativeToolRequest request,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken) =>
        request.ToolName switch
            {
                "read_file" => ExecuteReadFileAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "list_directory" => Task.FromResult(ExecuteListDirectory(runtimeProfile, arguments, approvalState)),
                "glob" => Task.FromResult(ExecuteGlob(runtimeProfile, arguments, approvalState)),
                "grep_search" => Task.FromResult(ExecuteGrep(runtimeProfile, arguments, approvalState)),
                "run_shell_command" => ExecuteShellAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "write_file" => ExecuteWriteFileAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "edit" => ExecuteEditAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "todo_write" => ExecuteTodoWriteAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "save_memory" => ExecuteSaveMemoryAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "agent" => agents.ExecuteAsync(paths, runtimeProfile, arguments, approvalState, eventSink, cancellationToken),
                "skill" => ExecuteSkillAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "exit_plan_mode" => Task.FromResult(ExecuteExitPlanMode(runtimeProfile, approvalState)),
                "web_fetch" => ExecuteWebFetchAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "web_search" => ExecuteWebSearchAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "mcp-client" => ExecuteMcpClientAsync(paths, runtimeProfile, arguments, approvalState, cancellationToken),
                "mcp-tool" => ExecuteMcpToolAsync(paths, runtimeProfile, arguments, approvalState, cancellationToken),
                "lsp" => lspTools.ExecuteAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "ask_user_question" => Task.FromResult(ExecuteAskUserQuestion(runtimeProfile, runtimeProfile.ProjectRoot, arguments, approvalState)),
                "cron_create" => Task.FromResult(ExecuteCronCreate(arguments, runtimeProfile, approvalState)),
                "cron_list" => Task.FromResult(ExecuteCronList(runtimeProfile, approvalState)),
                "cron_delete" => Task.FromResult(ExecuteCronDelete(arguments, runtimeProfile, approvalState)),
                _ => Task.FromResult(Error(request.ToolName, "Tool is not implemented by the native .NET host yet.", runtimeProfile.ProjectRoot))
            };

    private async Task<NativeToolExecutionResult> ApplyPermissionRequestHooksAsync(
        QwenRuntimeProfile runtimeProfile,
        ExecuteNativeToolRequest request,
        JsonElement arguments,
        NativeToolExecutionResult result,
        CancellationToken cancellationToken)
    {
        var hookResult = await ExecuteHookAsync(
            runtimeProfile,
            request,
            arguments,
            HookEventName.PermissionRequest,
            result.ApprovalState,
            result.WorkingDirectory,
            result,
            cancellationToken);
        if (hookResult.IsBlocked)
        {
            return CreateHookBlockedToolResult(result.ToolName, result.WorkingDirectory, "deny", hookResult.BlockReason);
        }

        return ApplyHookMessages(result, hookResult.AggregateOutput);
    }

    private async Task<NativeToolExecutionResult> ApplyPostToolHooksAsync(
        QwenRuntimeProfile runtimeProfile,
        ExecuteNativeToolRequest request,
        JsonElement arguments,
        NativeToolExecutionResult result,
        CancellationToken cancellationToken)
    {
        var eventName = result.Status switch
        {
            "completed" => HookEventName.PostToolUse,
            "error" or "blocked" => HookEventName.PostToolUseFailure,
            _ => (HookEventName?)null
        };

        if (!eventName.HasValue)
        {
            return result;
        }

        var hookResult = await ExecuteHookAsync(
            runtimeProfile,
            request,
            arguments,
            eventName.Value,
            result.ApprovalState,
            result.WorkingDirectory,
            result,
            cancellationToken);
        return ApplyHookMessages(result, hookResult.AggregateOutput);
    }

    private async Task<HookLifecycleResult> ExecuteHookAsync(
        QwenRuntimeProfile runtimeProfile,
        ExecuteNativeToolRequest request,
        JsonElement arguments,
        HookEventName eventName,
        string approvalState,
        string workingDirectory,
        NativeToolExecutionResult? result = null,
        CancellationToken cancellationToken = default)
    {
        if (hooks is null)
        {
            return new HookLifecycleResult();
        }

        var sessionId = TryGetOptionalString(arguments, "session_id") ??
                        TryGetOptionalString(arguments, "sessionId") ??
                        string.Empty;
        var transcriptPath = string.IsNullOrWhiteSpace(sessionId)
            ? string.Empty
            : Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl");
        var metadata = new JsonObject
        {
            ["tool_contract_path"] = ToolContractCatalog.ByName.TryGetValue(request.ToolName, out var tool)
                ? tool.ContractPath
                : string.Empty
        };

        return await hooks.ExecuteAsync(
            runtimeProfile,
            new HookInvocationRequest
            {
                EventName = eventName,
                SessionId = sessionId,
                WorkingDirectory = workingDirectory,
                TranscriptPath = transcriptPath,
                ToolName = request.ToolName,
                ToolStatus = result?.Status ?? string.Empty,
                ApprovalState = approvalState,
                ToolArgumentsJson = request.ArgumentsJson,
                ToolOutput = result?.Output ?? string.Empty,
                ToolErrorMessage = result?.ErrorMessage ?? string.Empty,
                Reason = result?.ErrorMessage ?? string.Empty,
                Metadata = metadata
            },
            cancellationToken);
    }

    private static NativeToolExecutionResult ApplyHookMessages(
        NativeToolExecutionResult result,
        HookOutput aggregateOutput)
    {
        var note = BuildHookNote(aggregateOutput);
        if (string.IsNullOrWhiteSpace(note))
        {
            return result;
        }

        return string.Equals(result.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? new NativeToolExecutionResult
            {
                ToolName = result.ToolName,
                Status = result.Status,
                ApprovalState = result.ApprovalState,
                WorkingDirectory = result.WorkingDirectory,
                Output = string.IsNullOrWhiteSpace(result.Output) ? note : $"{result.Output}{Environment.NewLine}{Environment.NewLine}{note}",
                ErrorMessage = result.ErrorMessage,
                ExitCode = result.ExitCode,
                ChangedFiles = result.ChangedFiles,
                Questions = result.Questions,
                Answers = result.Answers
            }
            : new NativeToolExecutionResult
            {
                ToolName = result.ToolName,
                Status = result.Status,
                ApprovalState = result.ApprovalState,
                WorkingDirectory = result.WorkingDirectory,
                Output = result.Output,
                ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage) ? note : $"{result.ErrorMessage}{Environment.NewLine}{note}",
                ExitCode = result.ExitCode,
                ChangedFiles = result.ChangedFiles,
                Questions = result.Questions,
                Answers = result.Answers
            };
    }

    private static string BuildHookNote(HookOutput aggregateOutput)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(aggregateOutput.SystemMessage))
        {
            lines.Add(aggregateOutput.SystemMessage);
        }

        if (!string.IsNullOrWhiteSpace(aggregateOutput.AdditionalContext))
        {
            lines.Add(aggregateOutput.AdditionalContext);
        }

        return string.Join(Environment.NewLine, lines.Where(static value => !string.IsNullOrWhiteSpace(value)));
    }

    private static NativeToolExecutionResult CreateHookBlockedToolResult(
        string toolName,
        string workingDirectory,
        string approvalState,
        string reason) =>
        new()
        {
            ToolName = toolName,
            Status = "blocked",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            ErrorMessage = reason,
            ChangedFiles = []
        };

    private NativeToolExecutionResult ExecuteAskUserQuestion(
        QwenRuntimeProfile runtimeProfile,
        string workingDirectory,
        JsonElement arguments,
        string approvalState)
    {
        try
        {
            return userQuestions.CreatePendingResult(runtimeProfile, workingDirectory, arguments, approvalState);
        }
        catch (Exception exception)
        {
            return Error("ask_user_question", exception.Message, workingDirectory, approvalState);
        }
    }

    private static ApprovalCheckContext BuildApprovalContext(
        string toolName,
        string kind,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments)
    {
        var workingDirectory = TryGetString(arguments, "directory", out var directory)
            ? ResolvePath(directory, runtimeProfile.ProjectRoot)
            : TryGetString(arguments, "path", out var explicitPath) && Directory.Exists(ResolvePath(explicitPath, runtimeProfile.ProjectRoot))
                ? ResolvePath(explicitPath, runtimeProfile.ProjectRoot)
                : runtimeProfile.ProjectRoot;

        var domain = TryExtractDomain(arguments);
        var filePath = toolName switch
        {
            "save_memory" => MemoryStore.ResolveMemoryFilePath(runtimeProfile, TryGetOptionalString(arguments, "scope")),
            "todo_write" => TodoStore.ResolveTodoFilePath(
                runtimeProfile,
                TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId")),
            _ => TryExtractFilePath(arguments, runtimeProfile.ProjectRoot)
        };

        return new ApprovalCheckContext
        {
            ToolName = toolName,
            Kind = kind,
            ProjectRoot = runtimeProfile.ProjectRoot,
            WorkingDirectory = workingDirectory,
            Command = TryGetString(arguments, "command", out var command) ? command : null,
            FilePath = filePath,
            Domain = domain,
            Specifier = TryGetString(arguments, "agent_type", out var agentType)
                ? agentType
                : TryGetString(arguments, "skill_name", out var skillName)
                    ? skillName
                    : null
        };
    }

    private static string? TryExtractFilePath(JsonElement arguments, string workspaceRoot)
    {
        if (TryGetString(arguments, "file_path", out var filePath))
        {
            return ResolvePath(filePath, workspaceRoot);
        }

        if (TryGetString(arguments, "path", out var pathValue))
        {
            return ResolvePath(pathValue, workspaceRoot);
        }

        return null;
    }

    private static string? TryExtractDomain(JsonElement arguments)
    {
        if (!TryGetString(arguments, "url", out var url))
        {
            return null;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private async Task<McpApprovalEvaluation> EvaluateMcpToolApprovalAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (mcpTools is null)
        {
            return new McpApprovalEvaluation(
                null,
                Error("mcp-tool", "MCP runtime is not available in this host instance.", runtimeProfile.ProjectRoot));
        }

        if (!TryGetString(arguments, "server_name", out var serverName))
        {
            return new McpApprovalEvaluation(
                null,
                Error("mcp-tool", "Parameter 'server_name' is required.", runtimeProfile.ProjectRoot));
        }

        if (!TryGetString(arguments, "tool_name", out var toolName))
        {
            return new McpApprovalEvaluation(
                null,
                Error("mcp-tool", "Parameter 'tool_name' is required.", runtimeProfile.ProjectRoot));
        }

        try
        {
            var tool = await mcpTools.ResolveToolAsync(paths, serverName, toolName, cancellationToken);
            var decision = approvalPolicyService.Evaluate(
                new ApprovalCheckContext
                {
                    ToolName = tool.FullyQualifiedName,
                    Kind = tool.ReadOnlyHint ? "read" : "execute",
                    ProjectRoot = runtimeProfile.ProjectRoot,
                    WorkingDirectory = runtimeProfile.ProjectRoot,
                    Specifier = tool.FullyQualifiedName
                },
                runtimeProfile.ApprovalProfile);

            if (!decision.Reason.Contains("explicit", StringComparison.OrdinalIgnoreCase))
            {
                if (tool.ReadOnlyHint)
                {
                    decision = new ApprovalDecision
                    {
                        State = "allow",
                        Reason = $"Allowed because MCP tool '{tool.FullyQualifiedName}' is marked read-only."
                    };
                }
                else
                {
                    decision = new ApprovalDecision
                    {
                        State = "ask",
                        Reason = $"Requires confirmation for MCP tool '{tool.FullyQualifiedName}'."
                    };
                }
            }

            return new McpApprovalEvaluation(decision, null);
        }
        catch (Exception exception)
        {
            return new McpApprovalEvaluation(
                null,
                Error("mcp-tool", exception.Message, runtimeProfile.ProjectRoot));
        }
    }

    private static async Task<NativeToolExecutionResult> ExecuteReadFileAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = RequirePath(arguments, "file_path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (filePath.IsError)
        {
            return Error("read_file", filePath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!File.Exists(filePath.Value))
        {
            return Error("read_file", "File not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var content = await File.ReadAllTextAsync(filePath.Value!, cancellationToken);
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var offset = TryGetInt(arguments, "offset") ?? 0;
        var limit = TryGetInt(arguments, "limit") ?? lines.Length;
        offset = Math.Max(0, offset);
        limit = Math.Max(1, limit);
        var slice = lines.Skip(offset).Take(limit).ToArray();

        return Success(
            "read_file",
            approvalState,
            runtimeProfile.ProjectRoot,
            string.Join(Environment.NewLine, slice),
            []);
    }

    private static NativeToolExecutionResult ExecuteListDirectory(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        var directoryPath = RequirePath(arguments, "path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (directoryPath.IsError)
        {
            return Error("list_directory", directoryPath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!Directory.Exists(directoryPath.Value))
        {
            return Error("list_directory", "Directory not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var entries = Directory.EnumerateFileSystemEntries(directoryPath.Value!)
            .Where(static path => !IgnoredDirectories.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .Take(100)
            .Select(static path =>
            {
                var isDirectory = Directory.Exists(path);
                return $"{(isDirectory ? "[DIR]" : "[FILE]")} {path}";
            })
            .ToArray();

        return Success("list_directory", approvalState, runtimeProfile.ProjectRoot, string.Join(Environment.NewLine, entries), []);
    }

    private static NativeToolExecutionResult ExecuteGlob(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        if (!TryGetString(arguments, "pattern", out var pattern))
        {
            return Error("glob", "Parameter 'pattern' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var searchRoot = TryGetString(arguments, "path", out var pathValue)
            ? ResolvePath(pathValue, runtimeProfile.ProjectRoot)
            : runtimeProfile.ProjectRoot;
        if (!Directory.Exists(searchRoot))
        {
            return Error("glob", "Search path does not exist.", runtimeProfile.ProjectRoot, approvalState);
        }

        var regex = BuildGlobRegex(pattern);
        var matches = EnumerateWorkspaceFiles(searchRoot)
            .Where(path => regex.IsMatch(Path.GetRelativePath(searchRoot, path).Replace('\\', '/')))
            .Take(100)
            .ToArray();

        return Success("glob", approvalState, searchRoot, string.Join(Environment.NewLine, matches), []);
    }

    private static NativeToolExecutionResult ExecuteGrep(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        if (!TryGetString(arguments, "pattern", out var pattern))
        {
            return Error("grep_search", "Parameter 'pattern' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var searchRoot = TryGetString(arguments, "path", out var pathValue)
            ? ResolvePath(pathValue, runtimeProfile.ProjectRoot)
            : runtimeProfile.ProjectRoot;
        if (!Directory.Exists(searchRoot))
        {
            return Error("grep_search", "Search path does not exist.", runtimeProfile.ProjectRoot, approvalState);
        }

        var limit = Math.Max(1, TryGetInt(arguments, "limit") ?? 100);
        Regex patternRegex;
        try
        {
            patternRegex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.Compiled);
        }
        catch (Exception exception)
        {
            return Error("grep_search", $"Invalid regex: {exception.Message}", runtimeProfile.ProjectRoot, approvalState);
        }

        Regex? globRegex = null;
        if (TryGetString(arguments, "glob", out var glob))
        {
            globRegex = BuildGlobRegex(glob);
        }

        var results = new List<string>();
        foreach (var file in EnumerateWorkspaceFiles(searchRoot))
        {
            var relativePath = Path.GetRelativePath(searchRoot, file).Replace('\\', '/');
            if (globRegex is not null && !globRegex.IsMatch(relativePath))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!patternRegex.IsMatch(lines[index]))
                {
                    continue;
                }

                results.Add($"{file}:{index + 1}: {lines[index]}");
                if (results.Count >= limit)
                {
                    return Success("grep_search", approvalState, searchRoot, string.Join(Environment.NewLine, results), []);
                }
            }
        }

        return Success("grep_search", approvalState, searchRoot, string.Join(Environment.NewLine, results), []);
    }

    private async Task<NativeToolExecutionResult> ExecuteShellAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        if (!TryGetString(arguments, "command", out var command))
        {
            return Error("run_shell_command", "Parameter 'command' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var workingDirectory = TryGetString(arguments, "directory", out var directory)
            ? ResolvePath(directory, runtimeProfile.ProjectRoot)
            : runtimeProfile.ProjectRoot;
        if (!Directory.Exists(workingDirectory))
        {
            return Error("run_shell_command", "Working directory does not exist.", runtimeProfile.ProjectRoot, approvalState);
        }

        var shellResult = await shell.ExecuteAsync(
            new ShellCommandRequest
            {
                Command = command,
                WorkingDirectory = workingDirectory
            },
            cancellationToken);

        return new NativeToolExecutionResult
        {
            ToolName = "run_shell_command",
            Status = shellResult.Cancelled
                ? "cancelled"
                : shellResult.TimedOut
                    ? "timeout"
                    : shellResult.ExitCode == 0
                        ? "completed"
                        : "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            Output = shellResult.Output,
            ErrorMessage = shellResult.ErrorMessage,
            ExitCode = shellResult.ExitCode,
            ChangedFiles = []
        };
    }

    private static async Task<NativeToolExecutionResult> ExecuteWriteFileAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = RequirePath(arguments, "file_path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (filePath.IsError)
        {
            return Error("write_file", filePath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "content", out var content))
        {
            return Error("write_file", "Parameter 'content' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath.Value!)!);
        await File.WriteAllTextAsync(filePath.Value!, content, cancellationToken);

        return Success("write_file", approvalState, runtimeProfile.ProjectRoot, "File written.", [filePath.Value!]);
    }

    private static async Task<NativeToolExecutionResult> ExecuteEditAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var filePath = RequirePath(arguments, "file_path", runtimeProfile.ProjectRoot, absoluteOnly: true);
        if (filePath.IsError)
        {
            return Error("edit", filePath.ErrorMessage!, runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "old_string", out var oldString))
        {
            return Error("edit", "Parameter 'old_string' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "new_string", out var newString))
        {
            return Error("edit", "Parameter 'new_string' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var replaceAll = TryGetBool(arguments, "replace_all") ?? false;
        var currentContent = File.Exists(filePath.Value!) ? await File.ReadAllTextAsync(filePath.Value!, cancellationToken) : string.Empty;
        string updatedContent;
        if (string.IsNullOrEmpty(oldString) && !File.Exists(filePath.Value!))
        {
            updatedContent = newString;
        }
        else
        {
            if (!currentContent.Contains(oldString, StringComparison.Ordinal))
            {
                return Error("edit", "Target text was not found in the file.", runtimeProfile.ProjectRoot, approvalState);
            }

            updatedContent = replaceAll
                ? currentContent.Replace(oldString, newString, StringComparison.Ordinal)
                : ReplaceFirst(currentContent, oldString, newString);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath.Value!)!);
        await File.WriteAllTextAsync(filePath.Value!, updatedContent, cancellationToken);

        return Success("edit", approvalState, runtimeProfile.ProjectRoot, "Edit applied.", [filePath.Value!]);
    }

    private static async Task<NativeToolExecutionResult> ExecuteTodoWriteAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await TodoStore.SaveTodosAsync(runtimeProfile, arguments, cancellationToken);
            return Success("todo_write", approvalState, runtimeProfile.RuntimeBaseDirectory, result.Summary, [result.FilePath]);
        }
        catch (Exception exception)
        {
            return Error("todo_write", exception.Message, runtimeProfile.RuntimeBaseDirectory, approvalState);
        }
    }

    private static async Task<NativeToolExecutionResult> ExecuteSaveMemoryAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        if (!TryGetString(arguments, "fact", out var fact))
        {
            return Error("save_memory", "Parameter 'fact' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        try
        {
            var scope = TryGetOptionalString(arguments, "scope");
            var targetPath = await MemoryStore.SaveFactAsync(runtimeProfile, fact, scope, cancellationToken);
            var effectiveScope = string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase) ? "global" : "project";
            return Success(
                "save_memory",
                approvalState,
                runtimeProfile.ProjectRoot,
                $"Saved memory to {targetPath} ({effectiveScope} scope).",
                [targetPath]);
        }
        catch (Exception exception)
        {
            return Error("save_memory", exception.Message, runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private async Task<NativeToolExecutionResult> ExecuteSkillAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var output = await skills.LoadSkillContentAsync(runtimeProfile, arguments, cancellationToken);
            return Success("skill", approvalState, runtimeProfile.ProjectRoot, output, []);
        }
        catch (Exception exception)
        {
            return Error("skill", exception.Message, runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private static NativeToolExecutionResult ExecuteExitPlanMode(
        QwenRuntimeProfile runtimeProfile,
        string approvalState) =>
        Success(
            "exit_plan_mode",
            approvalState,
            runtimeProfile.ProjectRoot,
            "Plan mode exit requested in the native desktop runtime.",
            []);

    private async Task<NativeToolExecutionResult> ExecuteWebFetchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var output = await webTools.FetchAsync(runtimeProfile, arguments, cancellationToken);
            return Success("web_fetch", approvalState, runtimeProfile.ProjectRoot, output, []);
        }
        catch (Exception exception)
        {
            return Error("web_fetch", exception.Message, runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private async Task<NativeToolExecutionResult> ExecuteWebSearchAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var output = await webTools.SearchAsync(runtimeProfile, arguments, cancellationToken);
            return Success("web_search", approvalState, runtimeProfile.ProjectRoot, output, []);
        }
        catch (Exception exception)
        {
            return Error("web_search", exception.Message, runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private async Task<NativeToolExecutionResult> ExecuteMcpClientAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        if (mcpTools is null)
        {
            return Error("mcp-client", "MCP runtime is not available in this host instance.", runtimeProfile.ProjectRoot, approvalState);
        }

        try
        {
            if (TryGetString(arguments, "prompt_name", out var promptName))
            {
                if (!TryGetString(arguments, "server_name", out var promptServerName))
                {
                    return Error("mcp-client", "Parameter 'server_name' is required when invoking an MCP prompt.", runtimeProfile.ProjectRoot, approvalState);
                }

                var prompt = await mcpTools.GetPromptAsync(paths, promptServerName, promptName, arguments, cancellationToken);
                return Success("mcp-client", approvalState, runtimeProfile.ProjectRoot, prompt.Output, []);
            }

            if (TryGetString(arguments, "uri", out var resourceUri))
            {
                if (!TryGetString(arguments, "server_name", out var serverName))
                {
                    return Error("mcp-client", "Parameter 'server_name' is required when reading an MCP resource.", runtimeProfile.ProjectRoot, approvalState);
                }

                var resource = await mcpTools.ReadResourceAsync(paths, serverName, resourceUri, cancellationToken);
                return Success("mcp-client", approvalState, runtimeProfile.ProjectRoot, resource.Output, []);
            }

            var output = await mcpTools.DescribeAsync(paths, arguments, cancellationToken);
            return Success("mcp-client", approvalState, runtimeProfile.ProjectRoot, output, []);
        }
        catch (Exception exception)
        {
            return Error("mcp-client", exception.Message, runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private async Task<NativeToolExecutionResult> ExecuteMcpToolAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        if (mcpTools is null)
        {
            return Error("mcp-tool", "MCP runtime is not available in this host instance.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "server_name", out var serverName))
        {
            return Error("mcp-tool", "Parameter 'server_name' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "tool_name", out var toolName))
        {
            return Error("mcp-tool", "Parameter 'tool_name' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        try
        {
            var result = await mcpTools.InvokeAsync(paths, serverName, toolName, arguments, cancellationToken);
            return new NativeToolExecutionResult
            {
                ToolName = "mcp-tool",
                Status = result.IsError ? "error" : "completed",
                ApprovalState = approvalState,
                WorkingDirectory = runtimeProfile.ProjectRoot,
                Output = result.Output,
                ErrorMessage = result.IsError ? result.Output : string.Empty,
                ChangedFiles = []
            };
        }
        catch (Exception exception)
        {
            return Error("mcp-tool", exception.Message, runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private NativeToolExecutionResult ExecuteCronCreate(
        JsonElement arguments,
        QwenRuntimeProfile runtimeProfile,
        string approvalState)
    {
        if (!TryGetString(arguments, "cron", out var cronExpression))
        {
            return Error("cron_create", "Parameter 'cron' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetString(arguments, "prompt", out var prompt))
        {
            return Error("cron_create", "Parameter 'prompt' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        var recurring = TryGetBool(arguments, "recurring") ?? true;

        try
        {
            var job = cron.Create(cronExpression, prompt, recurring);
            var output = recurring
                ? $"Scheduled recurring job {job.Id} ({job.CronExpression}). Session-only (not written to disk, dies when Qwen Code exits). Auto-expires after 3 days. Use CronDelete to cancel sooner."
                : $"Scheduled one-shot task {job.Id} ({job.CronExpression}). Session-only (not written to disk, dies when Qwen Code exits). It will fire once then auto-delete.";

            return Success("cron_create", approvalState, runtimeProfile.ProjectRoot, output, []);
        }
        catch (Exception exception)
        {
            return Error("cron_create", $"Error creating cron job: {exception.Message}", runtimeProfile.ProjectRoot, approvalState);
        }
    }

    private NativeToolExecutionResult ExecuteCronList(
        QwenRuntimeProfile runtimeProfile,
        string approvalState)
    {
        var jobs = cron.List();
        if (jobs.Count == 0)
        {
            return Success("cron_list", approvalState, runtimeProfile.ProjectRoot, "No active cron jobs.", []);
        }

        var lines = jobs.Select(static job =>
        {
            var type = job.IsRecurring ? "recurring" : "one-shot";
            return $"{job.Id} - {job.CronExpression} ({type}) [session-only]: {job.Prompt}";
        });

        return Success("cron_list", approvalState, runtimeProfile.ProjectRoot, string.Join(Environment.NewLine, lines), []);
    }

    private NativeToolExecutionResult ExecuteCronDelete(
        JsonElement arguments,
        QwenRuntimeProfile runtimeProfile,
        string approvalState)
    {
        if (!TryGetString(arguments, "id", out var id))
        {
            return Error("cron_delete", "Parameter 'id' is required.", runtimeProfile.ProjectRoot, approvalState);
        }

        return cron.Delete(id)
            ? Success("cron_delete", approvalState, runtimeProfile.ProjectRoot, $"Cancelled job {id}.", [])
            : Error("cron_delete", $"Job {id} not found.", runtimeProfile.ProjectRoot, approvalState);
    }

    private static NativeToolExecutionResult Success(
        string toolName,
        string approvalState,
        string workingDirectory,
        string output,
        IReadOnlyList<string> changedFiles) =>
        new()
        {
            ToolName = toolName,
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            Output = output,
            ChangedFiles = changedFiles
        };

    private static NativeToolExecutionResult Error(
        string toolName,
        string message,
        string workingDirectory,
        string approvalState = "deny") =>
        new()
        {
            ToolName = toolName,
            Status = "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            ErrorMessage = message,
            ChangedFiles = []
        };

    private static PathResolutionResult RequirePath(
        JsonElement arguments,
        string propertyName,
        string workspaceRoot,
        bool absoluteOnly)
    {
        if (!TryGetString(arguments, propertyName, out var path))
        {
            return PathResolutionResult.Fail($"Parameter '{propertyName}' is required.");
        }

        if (absoluteOnly && !Path.IsPathRooted(path))
        {
            return PathResolutionResult.Fail($"Parameter '{propertyName}' must be an absolute path.");
        }

        var resolved = ResolvePath(path, workspaceRoot);
        if (!IsWithinWorkspace(resolved, workspaceRoot))
        {
            return PathResolutionResult.Fail("Path is outside the workspace root.");
        }

        return PathResolutionResult.Success(resolved);
    }

    private static bool IsWithinWorkspace(string path, string workspaceRoot)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(workspaceRoot);
        return fullPath.StartsWith(fullRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string ResolvePath(string path, string workspaceRoot) =>
        Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspaceRoot, path));

    private static IEnumerable<string> EnumerateWorkspaceFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => IgnoredDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase)));

    private static bool TryGetString(JsonElement arguments, string propertyName, out string value)
    {
        value = string.Empty;
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? TryGetOptionalString(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? TryGetInt(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;

    private static bool? TryGetBool(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) &&
        (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;

    private static Regex BuildGlobRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '*')
            {
                var isDoubleStar = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (isDoubleStar)
                {
                    builder.Append(".*");
                    index++;
                }
                else
                {
                    builder.Append(@"[^/\\]*");
                }
            }
            else if (character == '?')
            {
                builder.Append('.');
            }
            else
            {
                builder.Append(Regex.Escape(character.ToString()));
            }
        }

        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static string ReplaceFirst(string content, string oldString, string newString)
    {
        var index = content.IndexOf(oldString, StringComparison.Ordinal);
        if (index < 0)
        {
            return content;
        }

        return string.Concat(
            content.AsSpan(0, index),
            newString,
            content.AsSpan(index + oldString.Length));
    }

    private sealed record PathResolutionResult(string? Value, string? ErrorMessage)
    {
        public bool IsError => ErrorMessage is not null;

        public static PathResolutionResult Success(string value) => new(value, null);

        public static PathResolutionResult Fail(string errorMessage) => new(null, errorMessage);
    }

    private sealed class DefaultDesktopEnvironmentPaths : IDesktopEnvironmentPaths
    {
        public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        public string? ProgramDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        public string CurrentDirectory => Environment.CurrentDirectory;

        public string AppBaseDirectory => AppContext.BaseDirectory;
    }

    private static ISubagentCoordinator CreateFallbackSubagentCoordinator(
        QwenRuntimeProfileService runtimeProfileService,
        IApprovalPolicyEngine approvalPolicyService)
    {
        var environmentPaths = new DefaultDesktopEnvironmentPaths();
        return new SubagentCoordinatorService(
            new SubagentCatalogService(environmentPaths),
            new ToolCatalogService(runtimeProfileService, approvalPolicyService),
            new QwenCompatibilityService(environmentPaths));
    }

    private sealed record McpApprovalEvaluation(ApprovalDecision? Decision, NativeToolExecutionResult? ErrorResult);
}
