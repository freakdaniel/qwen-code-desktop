using QwenCode.Core.Agents;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Hooks;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Mcp;
using QwenCode.Core.Models;
using QwenCode.Core.Permissions;
using QwenCode.Core.Runtime;
using QwenCode.Core.Telemetry;

namespace QwenCode.Core.Tools;

/// <summary>
/// Represents the Native Tool Host Service
/// </summary>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="approvalPolicyService">The approval policy service</param>
/// <param name="cronScheduler">The cron scheduler</param>
/// <param name="webToolService">The web tool service</param>
/// <param name="userQuestionToolService">The user question tool service</param>
/// <param name="mcpToolRuntime">The mcp tool runtime</param>
/// <param name="lspToolService">The lsp tool service</param>
/// <param name="skillToolService">The skill tool service</param>
/// <param name="subagentCoordinator">The subagent coordinator</param>
/// <param name="agentArenaService">The agent arena service</param>
/// <param name="hookLifecycleService">The hook lifecycle service</param>
/// <param name="shellExecutionService">The shell execution service</param>
/// <param name="telemetryService">The telemetry service</param>
/// <param name="approvalSessionRuleStore">The approval session rule store</param>
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
    IAgentArenaService? agentArenaService = null,
    IHookLifecycleService? hookLifecycleService = null,
    IShellExecutionService? shellExecutionService = null,
    ITelemetryService? telemetryService = null,
    IApprovalSessionRuleStore? approvalSessionRuleStore = null) : IToolExecutor
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
    private readonly IAgentArenaService? arena = agentArenaService;

    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting native tool host snapshot</returns>
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
                    ApprovalReason = approval.Reason,
                    IsEnabled = !approval.IsWholeToolDenyRule,
                    IsExplicitAskRule = approval.IsExplicitAskRule
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

    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to native tool execution result</returns>
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
            var approvalProfile = approvalSessionRuleStore?.Apply(runtimeProfile.ApprovalProfile, request.SessionId) ??
                                  runtimeProfile.ApprovalProfile;
            var approval = approvalPolicyService.Evaluate(approvalContext, approvalProfile);
            if (string.Equals(request.ToolName, "mcp-tool", StringComparison.OrdinalIgnoreCase))
            {
                var mcpApproval = await EvaluateMcpToolApprovalAsync(paths, runtimeProfile, approvalProfile, document.RootElement, cancellationToken);
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
                    IsExplicitAskRule = approval.IsExplicitAskRule,
                    MatchedApprovalRule = approval.MatchedRule,
                    ChangedFiles = []
                };
            }

            if (string.Equals(request.ToolName, "ask_user_question", StringComparison.OrdinalIgnoreCase))
            {
                var questionResult = ExecuteAskUserQuestion(runtimeProfile, approvalContext.WorkingDirectory ?? runtimeProfile.ProjectRoot, document.RootElement, approval.State);
                var askedResult = await ApplyPostToolHooksAsync(runtimeProfile, request, document.RootElement, questionResult, cancellationToken);
                await TrackTelemetryAsync(runtimeProfile, request, askedResult, 0, approval.State, cancellationToken);
                return askedResult;
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
                    IsExplicitAskRule = approval.IsExplicitAskRule,
                    MatchedApprovalRule = approval.MatchedRule,
                    ChangedFiles = []
                };
                var gatedResult = await ApplyPermissionRequestHooksAsync(runtimeProfile, request, document.RootElement, approvalRequiredResult, cancellationToken);
                await TrackTelemetryAsync(runtimeProfile, request, gatedResult, 0, approval.State, cancellationToken);
                return gatedResult;
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

            var stopwatch = Stopwatch.StartNew();
            var result = await ExecuteToolCoreAsync(paths, request, runtimeProfile, document.RootElement, approval.State, eventSink, cancellationToken);
            var finalResult = await ApplyPostToolHooksAsync(runtimeProfile, request, document.RootElement, result, cancellationToken);
            await TrackTelemetryAsync(runtimeProfile, request, finalResult, stopwatch.ElapsedMilliseconds, approval.State, cancellationToken);
            return finalResult;
        }
    }

    private async Task TrackTelemetryAsync(
        QwenRuntimeProfile runtimeProfile,
        ExecuteNativeToolRequest request,
        NativeToolExecutionResult result,
        long durationMs,
        string approvalState,
        CancellationToken cancellationToken)
    {
        if (telemetryService is null)
        {
            return;
        }

        await telemetryService.TrackToolCallAsync(
            runtimeProfile,
            ExtractSessionId(request.ArgumentsJson),
            new AssistantToolCall
            {
                Id = $"{request.ToolName}-{Guid.NewGuid():N}",
                ToolName = request.ToolName,
                ArgumentsJson = string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson
            },
            result,
            durationMs,
            ResolveToolType(request.ToolName),
            ResolveDecision(request, result, approvalState),
            ResolveMcpServerName(request.ArgumentsJson),
            cancellationToken);
    }

    private static string ExtractSessionId(string argumentsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            return TryGetOptionalString(document.RootElement, "session_id") ??
                   TryGetOptionalString(document.RootElement, "sessionId") ??
                   string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveToolType(string toolName) =>
        toolName.StartsWith("mcp-", StringComparison.OrdinalIgnoreCase) ? "mcp" : "native";

    private static string? ResolveDecision(
        ExecuteNativeToolRequest request,
        NativeToolExecutionResult result,
        string approvalState) =>
        result.Status switch
        {
            "approval-required" => null,
            "blocked" => "reject",
            _ when request.ApproveExecution => "accept",
            _ when string.Equals(approvalState, "allow", StringComparison.OrdinalIgnoreCase) => "auto_accept",
            _ => null
        };

    private static string? ResolveMcpServerName(string argumentsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            return TryGetOptionalString(document.RootElement, "server") ??
                   TryGetOptionalString(document.RootElement, "serverName");
        }
        catch
        {
            return null;
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
                "task_create" => ExecuteTaskCreateAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "task_list" => ExecuteTaskListAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "task_get" => ExecuteTaskGetAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "task_update" => ExecuteTaskUpdateAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "task_stop" => ExecuteTaskStopAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "save_memory" => ExecuteSaveMemoryAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "agent" => agents.ExecuteAsync(paths, runtimeProfile, arguments, approvalState, eventSink, cancellationToken),
                "arena" => ExecuteArenaAsync(paths, runtimeProfile, arguments, approvalState, eventSink, cancellationToken),
                "skill" => ExecuteSkillAsync(runtimeProfile, arguments, approvalState, cancellationToken),
                "tool_search" => Task.FromResult(ExecuteToolSearch(paths, runtimeProfile, arguments, approvalState)),
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

    private Task<NativeToolExecutionResult> ExecuteArenaAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken) =>
        arena is null
            ? Task.FromResult(Error("arena", "Arena runtime is not available in this host instance.", runtimeProfile.ProjectRoot, approvalState))
            : arena.ExecuteAsync(paths, runtimeProfile, arguments, approvalState, eventSink, cancellationToken);

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
            "task_create" or "task_list" or "task_get" or "task_update" or "task_stop" => TaskStore.ResolveTaskFilePath(
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
        ApprovalProfile approvalProfile,
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
                approvalProfile);

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

    private static async Task<NativeToolExecutionResult> ExecuteTaskCreateAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await TaskStore.CreateTaskAsync(runtimeProfile, arguments, cancellationToken);
            return Success(
                "task_create",
                approvalState,
                runtimeProfile.RuntimeBaseDirectory,
                $"Created task #{result.Task.Id}: {result.Task.Subject} [{result.Task.Status}].",
                [result.FilePath]);
        }
        catch (Exception exception)
        {
            return Error("task_create", exception.Message, runtimeProfile.RuntimeBaseDirectory, approvalState);
        }
    }

    private static async Task<NativeToolExecutionResult> ExecuteTaskListAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await TaskStore.ListTasksAsync(runtimeProfile, arguments, cancellationToken);
            var statusFilter = TryGetOptionalString(arguments, "status");
            var tasks = string.IsNullOrWhiteSpace(statusFilter)
                ? result.Tasks
                : result.Tasks.Where(task => string.Equals(task.Status, statusFilter, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (tasks.Count == 0)
            {
                return Success("task_list", approvalState, runtimeProfile.RuntimeBaseDirectory, "No tasks found.", [result.FilePath]);
            }

            var lines = new List<string> { $"Task list: {tasks.Count} task(s)." };
            lines.AddRange(tasks.Select(static task =>
            {
                var owner = string.IsNullOrWhiteSpace(task.Owner) ? string.Empty : $" ({task.Owner})";
                var blocked = task.BlockedBy.Count == 0 ? string.Empty : $" blocked by [{string.Join(", ", task.BlockedBy)}]";
                return $"- #{task.Id} [{task.Status}] {task.Subject}{owner}{blocked}";
            }));
            return Success("task_list", approvalState, runtimeProfile.RuntimeBaseDirectory, string.Join(Environment.NewLine, lines), [result.FilePath]);
        }
        catch (Exception exception)
        {
            return Error("task_list", exception.Message, runtimeProfile.RuntimeBaseDirectory, approvalState);
        }
    }

    private static async Task<NativeToolExecutionResult> ExecuteTaskGetAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var task = await TaskStore.GetTaskAsync(runtimeProfile, arguments, cancellationToken);
            var filePath = TaskStore.ResolveTaskFilePath(
                runtimeProfile,
                TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId"));

            if (task is null)
            {
                return Success("task_get", approvalState, runtimeProfile.RuntimeBaseDirectory, "Task not found.", [filePath]);
            }

            var lines = new List<string>
            {
                $"Task #{task.Id}: {task.Subject}",
                $"Status: {task.Status}",
                $"Description: {task.Description}"
            };

            if (!string.IsNullOrWhiteSpace(task.ActiveForm))
            {
                lines.Add($"Active form: {task.ActiveForm}");
            }

            if (!string.IsNullOrWhiteSpace(task.Owner))
            {
                lines.Add($"Owner: {task.Owner}");
            }

            if (task.BlockedBy.Count > 0)
            {
                lines.Add($"Blocked by: {string.Join(", ", task.BlockedBy)}");
            }

            if (task.Blocks.Count > 0)
            {
                lines.Add($"Blocks: {string.Join(", ", task.Blocks)}");
            }

            return Success("task_get", approvalState, runtimeProfile.RuntimeBaseDirectory, string.Join(Environment.NewLine, lines), [filePath]);
        }
        catch (Exception exception)
        {
            return Error("task_get", exception.Message, runtimeProfile.RuntimeBaseDirectory, approvalState);
        }
    }

    private static async Task<NativeToolExecutionResult> ExecuteTaskUpdateAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await TaskStore.UpdateTaskAsync(runtimeProfile, arguments, cancellationToken);
            if (result.Task is null)
            {
                return Success("task_update", approvalState, runtimeProfile.RuntimeBaseDirectory, "Task not found.", [result.FilePath]);
            }

            var updatedFields = result.UpdatedFields.Count == 0
                ? "No fields changed."
                : $"Updated fields: {string.Join(", ", result.UpdatedFields)}.";
            return Success(
                "task_update",
                approvalState,
                runtimeProfile.RuntimeBaseDirectory,
                $"Updated task #{result.Task.Id}: {result.Task.Subject} [{result.Task.Status}]. {updatedFields}",
                [result.FilePath]);
        }
        catch (Exception exception)
        {
            return Error("task_update", exception.Message, runtimeProfile.RuntimeBaseDirectory, approvalState);
        }
    }

    private static async Task<NativeToolExecutionResult> ExecuteTaskStopAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await TaskStore.StopTaskAsync(runtimeProfile, arguments, cancellationToken);
            if (result.Task is null)
            {
                return Success("task_stop", approvalState, runtimeProfile.RuntimeBaseDirectory, "Task not found.", [result.FilePath]);
            }

            return Success(
                "task_stop",
                approvalState,
                runtimeProfile.RuntimeBaseDirectory,
                $"Stopped task #{result.Task.Id}: {result.Task.Subject} [{result.Task.Status}].",
                [result.FilePath]);
        }
        catch (Exception exception)
        {
            return Error("task_stop", exception.Message, runtimeProfile.RuntimeBaseDirectory, approvalState);
        }
    }

    private NativeToolExecutionResult ExecuteToolSearch(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        var query = TryGetOptionalString(arguments, "query")?.Trim() ?? string.Empty;
        var kindFilter = TryGetOptionalString(arguments, "kind")?.Trim();
        var approvalFilter = TryGetOptionalString(arguments, "approval_state")?.Trim();
        var limit = Math.Clamp(TryGetInt(arguments, "limit") ?? 8, 1, 20);

        var rankedTools = Inspect(paths).Tools
            .Where(tool =>
                string.IsNullOrWhiteSpace(kindFilter) ||
                string.Equals(tool.Kind, kindFilter, StringComparison.OrdinalIgnoreCase))
            .Where(tool =>
                string.IsNullOrWhiteSpace(approvalFilter) ||
                string.Equals(tool.ApprovalState, approvalFilter, StringComparison.OrdinalIgnoreCase))
            .Select(tool => new
            {
                Tool = tool,
                Score = ScoreToolSearchMatch(tool, query)
            })
            .Where(item => string.IsNullOrWhiteSpace(query) || item.Score > 0)
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Tool.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

        if (rankedTools.Length == 0)
        {
            var emptyReason = string.IsNullOrWhiteSpace(query)
                ? "No tools matched the provided filters."
                : $"No tools matched query '{query}'.";
            return Success("tool_search", approvalState, runtimeProfile.ProjectRoot, emptyReason, []);
        }

        var lines = new List<string>
        {
            $"Tool search results: {rankedTools.Length} match(es)."
        };

        foreach (var item in rankedTools)
        {
            lines.Add(
                $"- `{item.Tool.Name}` [{item.Tool.Kind}, {item.Tool.ApprovalState}]: {DescribeToolSearchCandidate(item.Tool.Name)}");
        }

        return Success("tool_search", approvalState, runtimeProfile.ProjectRoot, string.Join(Environment.NewLine, lines), []);
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
            return Error("web_fetch", ResolveWebFetchErrorMessage(exception), runtimeProfile.ProjectRoot, approvalState);
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

    private static string ResolveWebFetchErrorMessage(Exception exception)
    {
        var baseException = exception.GetBaseException();

        if (baseException is TimeoutException)
        {
            return "The site did not respond in time.";
        }

        if (baseException is AuthenticationException ||
            exception is HttpRequestException httpRequestException &&
            httpRequestException.Message.Contains("SSL connection could not be established", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not establish a secure HTTPS connection to the target site. The site's TLS certificate or handshake appears to be invalid.";
        }

        if (baseException is OperationCanceledException)
        {
            return "The web request was cancelled before it finished.";
        }

        return string.IsNullOrWhiteSpace(exception.Message)
            ? "The web request failed."
            : exception.Message;
    }

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

    private static int ScoreToolSearchMatch(NativeToolRegistration tool, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 1;
        }

        var normalizedQuery = query.Trim();
        var description = DescribeToolSearchCandidate(tool.Name);
        var score = 0;

        if (tool.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (tool.DisplayName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (tool.Kind.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
        }

        if (description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        foreach (var token in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (tool.Name.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 4;
            }

            if (description.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += 2;
            }
        }

        return score;
    }

    private static string DescribeToolSearchCandidate(string toolName) =>
        toolName switch
        {
            "read_file" => "Read a file directly by absolute path.",
            "list_directory" => "Inspect the contents of a directory.",
            "glob" => "Find files by glob pattern.",
            "grep_search" => "Search file contents by regex or text pattern.",
            "run_shell_command" => "Run shell commands for build, test, git, or environment work.",
            "write_file" => "Write a full file to disk.",
            "edit" => "Apply targeted text edits inside an existing file.",
            "todo_write" => "Create or update a structured task list for the current session.",
            "save_memory" => "Persist durable user or project facts to memory files.",
            "agent" => "Delegate work to a subagent for parallel or specialized execution.",
            "arena" => "Run the same task across multiple models in an arena comparison.",
            "skill" => "Load a predefined skill workflow or instructions bundle.",
            "tool_search" => "Search the native tool catalog by intent, kind, or approval state.",
            "exit_plan_mode" => "Exit plan mode after preparing a concrete plan.",
            "web_fetch" => "Fetch and summarize a specific web page or URL.",
            "web_search" => "Search the web for recent or external information.",
            "mcp-client" => "Inspect connected MCP servers, discover prompts or resources, and invoke MCP prompts.",
            "mcp-tool" => "Execute a concrete tool exposed by a connected MCP server.",
            "lsp" => "Query semantic code intelligence such as symbols, definitions, references, implementations, diagnostics, or call hierarchy.",
            "ask_user_question" => "Pause and ask the user one or more structured follow-up questions.",
            "cron_create" => "Schedule a session-scoped recurring or one-shot automation.",
            "cron_list" => "List active session-scoped automation jobs.",
            "cron_delete" => "Cancel an active session-scoped automation job.",
            _ => "Native tool available in this desktop runtime."
        };

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
        /// <summary>
        /// Gets a value indicating whether is error
        /// </summary>
        public bool IsError => ErrorMessage is not null;

        /// <summary>
        /// Executes success
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The resulting path resolution result</returns>
        public static PathResolutionResult Success(string value) => new(value, null);

        /// <summary>
        /// Executes fail
        /// </summary>
        /// <param name="errorMessage">The error message</param>
        /// <returns>The resulting path resolution result</returns>
        public static PathResolutionResult Fail(string errorMessage) => new(null, errorMessage);
    }

    private sealed class DefaultDesktopEnvironmentPaths : IDesktopEnvironmentPaths
    {
        /// <summary>
        /// Gets the home directory
        /// </summary>
        public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        /// <summary>
        /// Gets the program data directory
        /// </summary>
        public string? ProgramDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        /// <summary>
        /// Gets the current directory
        /// </summary>
        public string CurrentDirectory => Environment.CurrentDirectory;

        /// <summary>
        /// Gets the app base directory
        /// </summary>
        public string AppBaseDirectory => AppContext.BaseDirectory;
    }

    private static ISubagentCoordinator CreateFallbackSubagentCoordinator(
        QwenRuntimeProfileService runtimeProfileService,
        IApprovalPolicyEngine approvalPolicyService)
    {
        var environmentPaths = new DefaultDesktopEnvironmentPaths();
        var modelSelectionService = new SubagentModelSelectionService();
        var validationService = new SubagentValidationService(modelSelectionService);
        return new SubagentCoordinatorService(
            new SubagentCatalogService(environmentPaths, validationService),
            new ToolCatalogService(runtimeProfileService, approvalPolicyService),
            new QwenCompatibilityService(environmentPaths),
            modelSelectionService,
            validationService);
    }

    private sealed record McpApprovalEvaluation(ApprovalDecision? Decision, NativeToolExecutionResult? ErrorResult);
}
