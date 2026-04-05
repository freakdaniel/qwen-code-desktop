using QwenCode.App.Models;
using QwenCode.App.Compatibility;
using QwenCode.App.Hooks;
using QwenCode.App.Runtime;
using QwenCode.App.Telemetry;
using QwenCode.App.Tools;
using System.Text.Json.Nodes;

namespace QwenCode.App.Sessions;

/// <summary>
/// Represents the Desktop Session Host Service
/// </summary>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="commandActionRuntime">The command action runtime</param>
/// <param name="assistantTurnRuntime">The assistant turn runtime</param>
/// <param name="chatCompressionService">The chat compression service</param>
/// <param name="chatRecordingService">The chat recording service</param>
/// <param name="nativeToolHostService">The native tool host service</param>
/// <param name="hookLifecycleService">The hook lifecycle service</param>
/// <param name="userQuestionToolService">The user question tool service</param>
/// <param name="userPromptHookService">The user prompt hook service</param>
/// <param name="sessionCatalogService">The session catalog service</param>
/// <param name="activeTurnRegistry">The active turn registry</param>
/// <param name="interruptedTurnStore">The interrupted turn store</param>
/// <param name="transcriptWriter">The transcript writer</param>
/// <param name="sessionEventFactory">The session event factory</param>
/// <param name="sessionMessageBus">The session message bus</param>
/// <param name="telemetryService">The telemetry service</param>
public sealed class DesktopSessionHostService(
    QwenRuntimeProfileService runtimeProfileService,
    ICommandActionRuntime commandActionRuntime,
    IAssistantTurnRuntime assistantTurnRuntime,
    IChatCompressionService chatCompressionService,
    IChatRecordingService chatRecordingService,
    IToolExecutor nativeToolHostService,
    IHookLifecycleService hookLifecycleService,
    IUserQuestionToolService userQuestionToolService,
    IUserPromptHookService userPromptHookService,
    ITranscriptStore sessionCatalogService,
    IActiveTurnRegistry activeTurnRegistry,
    IInterruptedTurnStore interruptedTurnStore,
    ISessionTranscriptWriter transcriptWriter,
    ISessionEventFactory sessionEventFactory,
    ISessionMessageBus sessionMessageBus,
    ITelemetryService? telemetryService = null) : ISessionHost
{
    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    public event EventHandler<DesktopSessionEvent>? SessionEvent;

    /// <summary>
    /// Starts turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public async Task<DesktopSessionTurnResult> StartTurnAsync(
        WorkspacePaths paths,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            throw new InvalidOperationException("Prompt is required to start a desktop session turn.");
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString() : request.SessionId;
        var normalizedRequest = new StartDesktopSessionTurnRequest
        {
            SessionId = sessionId,
            Prompt = request.Prompt,
            WorkingDirectory = request.WorkingDirectory,
            ToolName = request.ToolName,
            ToolArgumentsJson = request.ToolArgumentsJson,
            ApproveToolExecution = request.ApproveToolExecution
        };
        var workingDirectory = ResolveWorkingDirectory(runtimeProfile.ProjectRoot, normalizedRequest.WorkingDirectory);
        var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl");
        var createdNewSession = !File.Exists(transcriptPath);
        var gitBranch = TryReadGitBranch(workingDirectory);

        return await activeTurnRegistry.RunAsync(
            sessionId,
            CreateActiveTurnState(
                sessionId,
                normalizedRequest.Prompt,
                transcriptPath,
                workingDirectory,
                gitBranch,
                normalizedRequest.ToolName ?? string.Empty),
            token => StartTurnCoreAsync(paths, normalizedRequest, token),
            async () => await BuildCancelledTurnResultAsync(
                paths,
                sessionId,
                transcriptPath,
                workingDirectory,
                gitBranch,
                normalizedRequest.Prompt,
                CreateCancelledToolExecutionResult(workingDirectory),
                createdNewSession,
                resolvedCommand: null,
                cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Cancels turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    public async Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
        WorkspacePaths paths,
        CancelDesktopSessionTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to cancel a desktop session turn.");
        }

        if (activeTurnRegistry.Cancel(request.SessionId))
        {
            return await Task.FromResult(new CancelDesktopSessionTurnResult
            {
                SessionId = request.SessionId,
                Cancelled = true,
                Message = "Cancellation requested for the active desktop turn.",
                TimestampUtc = DateTime.UtcNow
            });
        }

        return await Task.FromResult(new CancelDesktopSessionTurnResult
        {
            SessionId = request.SessionId,
            Cancelled = false,
            Message = "No active desktop turn was found for this session.",
            TimestampUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public async Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
        WorkspacePaths paths,
        ResumeInterruptedTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to resume an interrupted desktop turn.");
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var interruptedTurn = interruptedTurnStore.Get(runtimeProfile.ChatsDirectory, request.SessionId)
            ?? throw new InvalidOperationException("No recoverable desktop turn was found for this session.");

        PublishSessionEvent(sessionEventFactory.CreateTurnReattached(
            request.SessionId,
            interruptedTurn.WorkingDirectory,
            interruptedTurn.GitBranch,
            interruptedTurn.ToolName));

        return await StartTurnAsync(
            paths,
            new StartDesktopSessionTurnRequest
            {
                SessionId = interruptedTurn.SessionId,
                Prompt = BuildRecoveryPrompt(interruptedTurn, request.RecoveryNote),
                WorkingDirectory = interruptedTurn.WorkingDirectory,
                ToolName = string.Empty,
                ToolArgumentsJson = "{}",
                ApproveToolExecution = false
            },
            cancellationToken);
    }

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
        WorkspacePaths paths,
        DismissInterruptedTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to dismiss a recoverable desktop turn.");
        }

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var dismissed = interruptedTurnStore.Remove(runtimeProfile.ChatsDirectory, request.SessionId);

        return Task.FromResult(new DismissInterruptedTurnResult
        {
            SessionId = request.SessionId,
            Dismissed = dismissed,
            Message = dismissed
                ? "Recoverable desktop turn dismissed."
                : "No recoverable desktop turn was found for this session.",
            TimestampUtc = DateTime.UtcNow
        });
    }

    private async Task<DesktopSessionTurnResult> StartTurnCoreAsync(
        WorkspacePaths paths,
        StartDesktopSessionTurnRequest request,
        CancellationToken cancellationToken)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var workingDirectory = ResolveWorkingDirectory(runtimeProfile.ProjectRoot, request.WorkingDirectory);
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString() : request.SessionId;
        var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl");
        var createdNewSession = !File.Exists(transcriptPath);
        var gitBranch = TryReadGitBranch(workingDirectory);
        var parentUuid = transcriptWriter.TryReadLastEntryUuid(transcriptPath);
        var timestampUtc = DateTime.UtcNow;
        Directory.CreateDirectory(runtimeProfile.ChatsDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (createdNewSession && telemetryService is not null)
        {
            await telemetryService.TrackSessionConfiguredAsync(runtimeProfile, sessionId, cancellationToken);
        }

        var sessionStartHook = await ExecuteLifecycleHookAsync(
            runtimeProfile,
            HookEventName.SessionStart,
            sessionId,
            workingDirectory,
            transcriptPath,
            prompt: request.Prompt,
            metadata: new JsonObject
            {
                ["trigger"] = createdNewSession ? "new" : "resume",
                ["source"] = createdNewSession ? "new" : "resume",
                ["model"] = "native-runtime",
                ["permission_mode"] = runtimeProfile.ApprovalProfile.DefaultMode
            },
            cancellationToken: cancellationToken);
        parentUuid = await AppendLifecycleHookContextEntriesAsync(
            transcriptPath,
            parentUuid,
            sessionId,
            workingDirectory,
            gitBranch,
            sessionStartHook,
            cancellationToken);
        if (sessionStartHook.IsBlocked)
        {
            return await BuildBlockedTurnResultAsync(
                paths,
                sessionId,
                transcriptPath,
                workingDirectory,
                gitBranch,
                request.Prompt,
                request.Prompt,
                CreatePromptHookResult(request.Prompt, sessionStartHook),
                createdNewSession,
                parentUuid,
                timestampUtc,
                cancellationToken);
        }

        var hookResult = await userPromptHookService.ExecuteAsync(
            runtimeProfile,
            new UserPromptHookRequest
            {
                SessionId = sessionId,
                Prompt = request.Prompt,
                WorkingDirectory = workingDirectory,
                TranscriptPath = transcriptPath
            },
            cancellationToken);
        var effectivePrompt = hookResult.EffectivePrompt;

        parentUuid = await AppendHookContextEntriesAsync(
            transcriptPath,
            parentUuid,
            sessionId,
            workingDirectory,
            gitBranch,
            hookResult,
            cancellationToken);

        parentUuid = await AppendCompressionCheckpointAsync(
            runtimeProfile,
            transcriptPath,
            parentUuid,
            sessionId,
            workingDirectory,
            gitBranch,
            cancellationToken);

        if (hookResult.IsBlocked)
        {
            return await BuildBlockedTurnResultAsync(
                paths,
                sessionId,
                transcriptPath,
                workingDirectory,
                gitBranch,
                request.Prompt,
                effectivePrompt,
                hookResult,
                createdNewSession,
                parentUuid,
                timestampUtc,
                cancellationToken);
        }

        PublishSessionEvent(sessionEventFactory.CreateTurnStarted(
            sessionId,
            effectivePrompt,
            workingDirectory,
            gitBranch,
            request.ToolName ?? string.Empty));

        var commandInvocation = await commandActionRuntime.TryInvokeAsync(paths, effectivePrompt, workingDirectory, cancellationToken);
        var resolvedCommand = commandInvocation?.Command;

        var userUuid = Guid.NewGuid().ToString();
        await transcriptWriter.AppendEntryAsync(
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
                            text = effectivePrompt
                        }
                    }
                }
            },
            cancellationToken);

        if (telemetryService is not null)
        {
            await telemetryService.TrackUserPromptAsync(
                runtimeProfile,
                sessionId,
                userUuid,
                effectivePrompt,
                runtimeProfile.Telemetry?.Target ?? string.Empty,
                cancellationToken);
        }

        parentUuid = userUuid;
        if (commandInvocation is not null)
        {
            var commandUuid = Guid.NewGuid().ToString();
            await transcriptWriter.AppendEntryAsync(
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

            PublishSessionEvent(sessionEventFactory.CreateCommandCompleted(
                sessionId,
                commandInvocation,
                workingDirectory,
                gitBranch));
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
                cancellationToken: cancellationToken);

            var toolUuid = Guid.NewGuid().ToString();
            await transcriptWriter.AppendEntryAsync(
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
                    changedFiles = toolExecution.ChangedFiles,
                    questions = toolExecution.Questions,
                    answers = toolExecution.Answers
                },
                cancellationToken);

            parentUuid = toolUuid;

            PublishSessionEvent(sessionEventFactory.CreateToolEvent(sessionId, toolExecution, gitBranch));
            activeTurnRegistry.Update(sessionId, state =>
            {
                state.ToolName = toolExecution.ToolName;
                state.Stage = "tool-executed";
                state.Status = toolExecution.Status;
            });
        }

        var assistantResponse = await assistantTurnRuntime.GenerateAsync(
            CreateAssistantTurnRequest(
                sessionId,
                effectivePrompt,
                workingDirectory,
                transcriptPath,
                runtimeProfile,
                gitBranch,
                commandInvocation,
                resolvedCommand,
                toolExecution,
                isApprovalResolution: false),
            runtimeEvent =>
            {
                activeTurnRegistry.Update(sessionId, state => ApplyRuntimeEvent(state, runtimeEvent));
                PublishSessionEvent(sessionEventFactory.CreateAssistantRuntimeEvent(
                    sessionId,
                    runtimeEvent,
                    workingDirectory,
                    gitBranch,
                    resolvedCommand?.Name ?? string.Empty,
                    toolExecution.ToolName));
            },
            cancellationToken);
        parentUuid = await transcriptWriter.AppendAssistantToolExecutionsAsync(
            transcriptPath,
            sessionId,
            parentUuid,
            gitBranch,
            assistantResponse.ToolExecutions,
            cancellationToken);
        var stopHook = await ExecuteLifecycleHookAsync(
            runtimeProfile,
            HookEventName.Stop,
            sessionId,
            workingDirectory,
            transcriptPath,
            prompt: effectivePrompt,
            toolName: toolExecution.ToolName,
            toolStatus: toolExecution.Status,
            approvalState: toolExecution.ApprovalState,
            toolOutput: assistantResponse.Summary,
            metadata: new JsonObject
            {
                ["stop_hook_active"] = true,
                ["last_assistant_message"] = assistantResponse.Summary
            },
            cancellationToken: cancellationToken);
        parentUuid = await AppendLifecycleHookContextEntriesAsync(
            transcriptPath,
            parentUuid,
            sessionId,
            workingDirectory,
            gitBranch,
            stopHook,
            cancellationToken);
        var assistantSummary = ApplyStopHookSummary(assistantResponse.Summary, stopHook);
        var assistantTimestamp = DateTime.UtcNow;
        await transcriptWriter.AppendEntryAsync(
            transcriptPath,
            new
            {
                uuid = Guid.NewGuid().ToString(),
                parentUuid,
                sessionId,
                timestamp = assistantTimestamp,
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
                    },
                    provider = assistantResponse.ProviderName,
                    model = assistantResponse.Model
                }
            },
            cancellationToken);

        PublishSessionEvent(sessionEventFactory.CreateAssistantCompleted(
            sessionId,
            assistantSummary,
            workingDirectory,
            gitBranch,
            resolvedCommand?.Name ?? string.Empty,
            toolExecution.ToolName));
        activeTurnRegistry.Update(sessionId, state =>
        {
            state.Stage = "assistant-completed";
            state.Status = "completed";
            state.ContentSnapshot = assistantSummary;
        });

        await RefreshSessionRecordingAsync(
            transcriptPath,
            new SessionRecordingContext
            {
                SessionId = sessionId,
                WorkingDirectory = workingDirectory,
                GitBranch = gitBranch,
                TitleHint = effectivePrompt,
                Status = "completed"
            },
            cancellationToken);

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, sessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(sessionId, transcriptPath, workingDirectory, gitBranch, effectivePrompt);

        var result = new DesktopSessionTurnResult
        {
            Session = session,
            AssistantSummary = assistantSummary,
            CreatedNewSession = createdNewSession,
            ToolExecution = toolExecution,
            ResolvedCommand = resolvedCommand
        };

        PublishSessionEvent(sessionEventFactory.CreateTurnCompleted(
            sessionId,
            createdNewSession
                ? "Desktop session created and turn persisted."
                : "Desktop session updated and turn persisted.",
            workingDirectory,
            gitBranch,
            resolvedCommand?.Name ?? string.Empty,
            toolExecution.ToolName,
            "completed"));
        await ExecuteNotificationHookAsync(
            runtimeProfile,
            sessionId,
            workingDirectory,
            transcriptPath,
            "turn_completed",
            assistantSummary,
            cancellationToken);
        await ExecuteSessionEndHookAsync(
            runtimeProfile,
            sessionId,
            workingDirectory,
            transcriptPath,
            "completed",
            assistantSummary,
            cancellationToken);

        return result;
    }

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public async Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        WorkspacePaths paths,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to approve a pending tool.");
        }

        var approvalContext = await sessionMessageBus.RequestPendingToolApprovalAsync(new PendingToolApprovalMessageRequest
        {
            Paths = paths,
            SessionId = request.SessionId,
            EntryId = request.EntryId
        }, cancellationToken);
        var detail = approvalContext.Detail;
        var workingDirectory = approvalContext.WorkingDirectory;
        var gitBranch = approvalContext.GitBranch;

        return await activeTurnRegistry.RunAsync(
            request.SessionId,
            CreateActiveTurnState(
                request.SessionId,
                detail.Session.Title,
                detail.TranscriptPath,
                workingDirectory,
                gitBranch,
                string.Empty),
            token => ApprovePendingToolCoreAsync(paths, request, approvalContext, token),
            async () => await BuildCancelledTurnResultAsync(
                paths,
                request.SessionId,
                detail.TranscriptPath,
                workingDirectory,
                gitBranch,
                detail.Session.Title,
                CreateCancelledToolExecutionResult(workingDirectory),
                createdNewSession: false,
                resolvedCommand: null,
                cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public async Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
        WorkspacePaths paths,
        AnswerDesktopSessionQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to answer a pending question.");
        }

        var answerContext = await sessionMessageBus.RequestPendingQuestionAnswerAsync(new PendingQuestionAnswerMessageRequest
        {
            Paths = paths,
            SessionId = request.SessionId,
            EntryId = request.EntryId,
            Answers = request.Answers
        }, cancellationToken);
        var detail = answerContext.Detail;
        var workingDirectory = answerContext.WorkingDirectory;
        var gitBranch = answerContext.GitBranch;

        return await activeTurnRegistry.RunAsync(
            request.SessionId,
            CreateActiveTurnState(
                request.SessionId,
                detail.Session.Title,
                detail.TranscriptPath,
                workingDirectory,
                gitBranch,
                "ask_user_question"),
            token => AnswerPendingQuestionCoreAsync(paths, request, answerContext, token),
            async () => await BuildCancelledTurnResultAsync(
                paths,
                request.SessionId,
                detail.TranscriptPath,
                workingDirectory,
                gitBranch,
                detail.Session.Title,
                CreateCancelledToolExecutionResult(workingDirectory),
                createdNewSession: false,
                resolvedCommand: null,
                cancellationToken),
            cancellationToken);
    }

    private async Task<DesktopSessionTurnResult> ApprovePendingToolCoreAsync(
        WorkspacePaths paths,
        ApproveDesktopSessionToolRequest request,
        PendingToolApprovalMessageResponse approvalContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to approve a pending tool.");
        }

        var detail = approvalContext.Detail;
        var pendingTool = approvalContext.PendingTool;

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var execution = await nativeToolHostService.ExecuteAsync(
            paths,
            new ExecuteNativeToolRequest
            {
                ToolName = pendingTool.ToolName,
                ArgumentsJson = string.IsNullOrWhiteSpace(pendingTool.Arguments) ? "{}" : pendingTool.Arguments,
                ApproveExecution = true
            },
            cancellationToken: cancellationToken);

        var resolutionTimestamp = DateTime.UtcNow;
        await transcriptWriter.MarkToolEntryResolvedAsync(
            detail.TranscriptPath,
            pendingTool.Id,
            "approved",
            resolutionTimestamp,
            cancellationToken);

        var parentUuid = transcriptWriter.TryReadLastEntryUuid(detail.TranscriptPath);
        var gitBranch = approvalContext.GitBranch;
        var workingDirectory = approvalContext.WorkingDirectory;

        PublishSessionEvent(sessionEventFactory.CreateToolApproved(
            request.SessionId,
            pendingTool.ToolName,
            workingDirectory,
            gitBranch,
            resolutionTimestamp));
        activeTurnRegistry.Update(request.SessionId, state =>
        {
            state.ToolName = pendingTool.ToolName;
            state.Stage = "tool-approved";
            state.Status = "approved";
        });

        var toolUuid = Guid.NewGuid().ToString();
        await transcriptWriter.AppendEntryAsync(
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
                questions = execution.Questions,
                answers = execution.Answers,
                resolutionStatus = "executed-after-approval",
                sourcePath = pendingTool.SourcePath,
                scope = pendingTool.Scope
            },
            cancellationToken);

        PublishSessionEvent(sessionEventFactory.CreateToolEvent(request.SessionId, execution, gitBranch));
        activeTurnRegistry.Update(request.SessionId, state =>
        {
            state.ToolName = execution.ToolName;
            state.Stage = "tool-executed";
            state.Status = execution.Status;
        });

        var assistantResponse = await assistantTurnRuntime.GenerateAsync(
            CreateAssistantTurnRequest(
                request.SessionId,
                pendingTool.Body,
                workingDirectory,
                detail.TranscriptPath,
                runtimeProfile,
                gitBranch,
                commandInvocation: null,
                resolvedCommand: null,
                execution,
                isApprovalResolution: true),
            runtimeEvent =>
            {
                activeTurnRegistry.Update(request.SessionId, state => ApplyRuntimeEvent(state, runtimeEvent));
                PublishSessionEvent(sessionEventFactory.CreateAssistantRuntimeEvent(
                    request.SessionId,
                    runtimeEvent,
                    workingDirectory,
                    gitBranch,
                    string.Empty,
                    execution.ToolName));
            },
            cancellationToken);
        var assistantSummary = assistantResponse.Summary;
        parentUuid = await transcriptWriter.AppendAssistantToolExecutionsAsync(
            detail.TranscriptPath,
            request.SessionId,
            toolUuid,
            gitBranch,
            assistantResponse.ToolExecutions,
            cancellationToken);

        await transcriptWriter.AppendEntryAsync(
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
                    },
                    provider = assistantResponse.ProviderName,
                    model = assistantResponse.Model
                }
            },
            cancellationToken);

        PublishSessionEvent(sessionEventFactory.CreateAssistantCompleted(
            request.SessionId,
            assistantSummary,
            workingDirectory,
            gitBranch,
            string.Empty,
            execution.ToolName));
        activeTurnRegistry.Update(request.SessionId, state =>
        {
            state.Stage = "assistant-completed";
            state.Status = "completed";
            state.ContentSnapshot = assistantSummary;
        });

        await RefreshSessionRecordingAsync(
            detail.TranscriptPath,
            new SessionRecordingContext
            {
                SessionId = request.SessionId,
                WorkingDirectory = workingDirectory,
                GitBranch = gitBranch,
                TitleHint = pendingTool.Title,
                Status = execution.Status
            },
            cancellationToken);

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, request.SessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(request.SessionId, detail.TranscriptPath, workingDirectory, gitBranch, pendingTool.Title);

        var result = new DesktopSessionTurnResult
        {
            Session = session,
            AssistantSummary = assistantSummary,
            CreatedNewSession = false,
            ToolExecution = execution,
            ResolvedCommand = null
        };

        PublishSessionEvent(sessionEventFactory.CreateTurnCompleted(
            request.SessionId,
            "Pending tool approval resolved and desktop session updated.",
            workingDirectory,
            gitBranch,
            string.Empty,
            execution.ToolName,
            execution.Status));
        await ExecuteNotificationHookAsync(
            runtimeProfile,
            request.SessionId,
            workingDirectory,
            detail.TranscriptPath,
            "turn_completed",
            assistantSummary,
            cancellationToken);
        await ExecuteSessionEndHookAsync(
            runtimeProfile,
            request.SessionId,
            workingDirectory,
            detail.TranscriptPath,
            execution.Status,
            assistantSummary,
            cancellationToken);

        return result;
    }

    private async Task<DesktopSessionTurnResult> AnswerPendingQuestionCoreAsync(
        WorkspacePaths paths,
        AnswerDesktopSessionQuestionRequest request,
        PendingQuestionAnswerMessageResponse answerContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to answer a pending question.");
        }

        var detail = answerContext.Detail;
        var pendingQuestion = answerContext.PendingQuestion;
        var questions = answerContext.Questions;
        var answers = answerContext.Answers;

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var workingDirectory = answerContext.WorkingDirectory;
        var execution = userQuestionToolService.CreateAnsweredResult(
            workingDirectory,
            string.IsNullOrWhiteSpace(pendingQuestion.ApprovalState) ? "ask" : pendingQuestion.ApprovalState,
            questions,
            answers);

        var resolutionTimestamp = DateTime.UtcNow;
        await transcriptWriter.MarkToolEntryResolvedAsync(
            detail.TranscriptPath,
            pendingQuestion.Id,
            "answered",
            resolutionTimestamp,
            cancellationToken);

        var parentUuid = transcriptWriter.TryReadLastEntryUuid(detail.TranscriptPath);
        var gitBranch = answerContext.GitBranch;

        PublishSessionEvent(sessionEventFactory.CreateUserInputReceived(
            request.SessionId,
            execution.ToolName,
            workingDirectory,
            gitBranch,
            resolutionTimestamp));
        activeTurnRegistry.Update(request.SessionId, state =>
        {
            state.ToolName = execution.ToolName;
            state.Stage = "user-input-received";
            state.Status = "answered";
            state.ContentSnapshot = execution.Output;
        });

        var toolUuid = Guid.NewGuid().ToString();
        await transcriptWriter.AppendEntryAsync(
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
                args = string.IsNullOrWhiteSpace(pendingQuestion.Arguments) ? "{}" : pendingQuestion.Arguments,
                approvalState = execution.ApprovalState,
                status = execution.Status,
                output = execution.Output,
                errorMessage = execution.ErrorMessage,
                exitCode = execution.ExitCode,
                changedFiles = execution.ChangedFiles,
                questions = execution.Questions,
                answers = execution.Answers,
                resolutionStatus = "answered-by-user",
                sourcePath = pendingQuestion.SourcePath,
                scope = pendingQuestion.Scope
            },
            cancellationToken);

        PublishSessionEvent(sessionEventFactory.CreateToolEvent(request.SessionId, execution, gitBranch));
        activeTurnRegistry.Update(request.SessionId, state =>
        {
            state.ToolName = execution.ToolName;
            state.Stage = "tool-executed";
            state.Status = execution.Status;
            state.ContentSnapshot = execution.Output;
        });

        var assistantResponse = await assistantTurnRuntime.GenerateAsync(
            CreateAssistantTurnRequest(
                request.SessionId,
                execution.Output,
                workingDirectory,
                detail.TranscriptPath,
                runtimeProfile,
                gitBranch,
                commandInvocation: null,
                resolvedCommand: null,
                execution,
                isApprovalResolution: true),
            runtimeEvent =>
            {
                activeTurnRegistry.Update(request.SessionId, state => ApplyRuntimeEvent(state, runtimeEvent));
                PublishSessionEvent(sessionEventFactory.CreateAssistantRuntimeEvent(
                    request.SessionId,
                    runtimeEvent,
                    workingDirectory,
                    gitBranch,
                    string.Empty,
                    execution.ToolName));
            },
            cancellationToken);
        var assistantSummary = assistantResponse.Summary;
        parentUuid = await transcriptWriter.AppendAssistantToolExecutionsAsync(
            detail.TranscriptPath,
            request.SessionId,
            toolUuid,
            gitBranch,
            assistantResponse.ToolExecutions,
            cancellationToken);

        await transcriptWriter.AppendEntryAsync(
            detail.TranscriptPath,
            new
            {
                uuid = Guid.NewGuid().ToString(),
                parentUuid = parentUuid ?? toolUuid,
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
                    },
                    provider = assistantResponse.ProviderName,
                    model = assistantResponse.Model
                }
            },
            cancellationToken);

        PublishSessionEvent(sessionEventFactory.CreateAssistantCompleted(
            request.SessionId,
            assistantSummary,
            workingDirectory,
            gitBranch,
            string.Empty,
            execution.ToolName));
        activeTurnRegistry.Update(request.SessionId, state =>
        {
            state.Stage = "assistant-completed";
            state.Status = "completed";
            state.ContentSnapshot = assistantSummary;
        });

        await RefreshSessionRecordingAsync(
            detail.TranscriptPath,
            new SessionRecordingContext
            {
                SessionId = request.SessionId,
                WorkingDirectory = workingDirectory,
                GitBranch = gitBranch,
                TitleHint = pendingQuestion.Title,
                Status = execution.Status
            },
            cancellationToken);

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, request.SessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(request.SessionId, detail.TranscriptPath, workingDirectory, gitBranch, pendingQuestion.Title);

        var result = new DesktopSessionTurnResult
        {
            Session = session,
            AssistantSummary = assistantSummary,
            CreatedNewSession = false,
            ToolExecution = execution,
            ResolvedCommand = null
        };

        PublishSessionEvent(sessionEventFactory.CreateTurnCompleted(
            request.SessionId,
            "Captured user answers and updated the desktop session.",
            workingDirectory,
            gitBranch,
            string.Empty,
            execution.ToolName,
            execution.Status));
        await ExecuteNotificationHookAsync(
            runtimeProfile,
            request.SessionId,
            workingDirectory,
            detail.TranscriptPath,
            "turn_completed",
            assistantSummary,
            cancellationToken);
        await ExecuteSessionEndHookAsync(
            runtimeProfile,
            request.SessionId,
            workingDirectory,
            detail.TranscriptPath,
            execution.Status,
            assistantSummary,
            cancellationToken);

        return result;
    }

    private async Task<string?> AppendHookContextEntriesAsync(
        string transcriptPath,
        string? parentUuid,
        string sessionId,
        string workingDirectory,
        string gitBranch,
        UserPromptHookResult hookResult,
        CancellationToken cancellationToken)
    {
        var currentParentUuid = parentUuid;

        if (!string.IsNullOrWhiteSpace(hookResult.SystemMessage))
        {
            currentParentUuid = await AppendSystemEntryAsync(
                transcriptPath,
                currentParentUuid,
                sessionId,
                workingDirectory,
                gitBranch,
                hookResult.SystemMessage,
                "hook-system-message",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(hookResult.AdditionalContext))
        {
            currentParentUuid = await AppendSystemEntryAsync(
                transcriptPath,
                currentParentUuid,
                sessionId,
                workingDirectory,
                gitBranch,
                hookResult.AdditionalContext,
                "hook-additional-context",
                cancellationToken);
        }

        return currentParentUuid;
    }

    private async Task<string?> AppendLifecycleHookContextEntriesAsync(
        string transcriptPath,
        string? parentUuid,
        string sessionId,
        string workingDirectory,
        string gitBranch,
        HookLifecycleResult hookResult,
        CancellationToken cancellationToken)
    {
        var currentParentUuid = parentUuid;

        if (!string.IsNullOrWhiteSpace(hookResult.AggregateOutput.SystemMessage))
        {
            currentParentUuid = await AppendSystemEntryAsync(
                transcriptPath,
                currentParentUuid,
                sessionId,
                workingDirectory,
                gitBranch,
                hookResult.AggregateOutput.SystemMessage,
                "hook-system-message",
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(hookResult.AggregateOutput.AdditionalContext))
        {
            currentParentUuid = await AppendSystemEntryAsync(
                transcriptPath,
                currentParentUuid,
                sessionId,
                workingDirectory,
                gitBranch,
                hookResult.AggregateOutput.AdditionalContext,
                "hook-additional-context",
                cancellationToken);
        }

        return currentParentUuid;
    }

    private async Task<string?> AppendCompressionCheckpointAsync(
        QwenRuntimeProfile runtimeProfile,
        string transcriptPath,
        string? parentUuid,
        string sessionId,
        string workingDirectory,
        string gitBranch,
        CancellationToken cancellationToken)
    {
        var checkpoint = await chatCompressionService.TryCreateCheckpointAsync(runtimeProfile, transcriptPath, cancellationToken);
        if (checkpoint is null)
        {
            return parentUuid;
        }

        if (telemetryService is not null)
        {
            await telemetryService.TrackChatCompressionAsync(runtimeProfile, sessionId, checkpoint, cancellationToken);
        }

        _ = await hookLifecycleService.ExecuteAsync(
            runtimeProfile,
            new HookInvocationRequest
            {
                EventName = HookEventName.PreCompact,
                SessionId = sessionId,
                WorkingDirectory = workingDirectory,
                TranscriptPath = transcriptPath,
                Metadata =
                {
                    ["trigger"] = "auto",
                    ["compressedEntries"] = checkpoint.CompressedEntryCount,
                    ["preservedEntries"] = checkpoint.PreservedEntryCount,
                    ["estimatedTokens"] = checkpoint.EstimatedTokenCount,
                    ["contextWindowTokens"] = checkpoint.EstimatedContextWindowTokens,
                    ["contextPercentage"] = checkpoint.EstimatedContextPercentage,
                    ["thresholdPercentage"] = checkpoint.ThresholdPercentage
                }
            },
            cancellationToken);

        return await AppendSystemEntryAsync(
            transcriptPath,
            parentUuid,
            sessionId,
            workingDirectory,
            gitBranch,
            checkpoint.Summary,
            "chat-compression",
            cancellationToken);
    }

    private async Task<DesktopSessionTurnResult> BuildBlockedTurnResultAsync(
        WorkspacePaths paths,
        string sessionId,
        string transcriptPath,
        string workingDirectory,
        string gitBranch,
        string originalPrompt,
        string effectivePrompt,
        UserPromptHookResult hookResult,
        bool createdNewSession,
        string? parentUuid,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        var userUuid = Guid.NewGuid().ToString();
        await transcriptWriter.AppendEntryAsync(
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
                            text = effectivePrompt
                        }
                    }
                },
                originalPrompt
            },
            cancellationToken);

        var blockMessage = string.IsNullOrWhiteSpace(hookResult.BlockReason)
            ? "Prompt was blocked by a configured hook."
            : hookResult.BlockReason;
        await AppendSystemEntryAsync(
            transcriptPath,
            userUuid,
            sessionId,
            workingDirectory,
            gitBranch,
            blockMessage,
            "blocked",
            cancellationToken);

        await RefreshSessionRecordingAsync(
            transcriptPath,
            new SessionRecordingContext
            {
                SessionId = sessionId,
                WorkingDirectory = workingDirectory,
                GitBranch = gitBranch,
                TitleHint = effectivePrompt,
                Status = "blocked"
            },
            cancellationToken);

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, sessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(sessionId, transcriptPath, workingDirectory, gitBranch, effectivePrompt, "blocked");

        session = new SessionPreview
        {
            SessionId = session.SessionId,
            Title = session.Title,
            LastActivity = session.LastActivity,
            Category = session.Category,
            Mode = session.Mode,
            Status = "blocked",
            WorkingDirectory = session.WorkingDirectory,
            GitBranch = session.GitBranch,
            MessageCount = session.MessageCount,
            TranscriptPath = session.TranscriptPath,
            StartedAt = session.StartedAt,
            LastUpdatedAt = session.LastUpdatedAt,
            MetadataPath = session.MetadataPath
        };

        PublishSessionEvent(sessionEventFactory.CreateTurnCompleted(
            sessionId,
            blockMessage,
            workingDirectory,
            gitBranch,
            string.Empty,
            string.Empty,
            "blocked"));
        await ExecuteNotificationHookAsync(
            runtimeProfileService.Inspect(paths),
            sessionId,
            workingDirectory,
            transcriptPath,
            "turn_blocked",
            blockMessage,
            cancellationToken);
        await ExecuteSessionEndHookAsync(
            runtimeProfileService.Inspect(paths),
            sessionId,
            workingDirectory,
            transcriptPath,
            "blocked",
            blockMessage,
            cancellationToken);

        return new DesktopSessionTurnResult
        {
            Session = session,
            AssistantSummary = blockMessage,
            CreatedNewSession = createdNewSession,
            ToolExecution = CreateBlockedHookExecutionResult(workingDirectory, blockMessage),
            ResolvedCommand = null
        };
    }

    private async Task<string> AppendSystemEntryAsync(
        string transcriptPath,
        string? parentUuid,
        string sessionId,
        string workingDirectory,
        string gitBranch,
        string message,
        string status,
        CancellationToken cancellationToken)
    {
        var systemUuid = Guid.NewGuid().ToString();
        await transcriptWriter.AppendEntryAsync(
            transcriptPath,
            new
            {
                uuid = systemUuid,
                parentUuid,
                sessionId,
                timestamp = DateTime.UtcNow,
                type = "system",
                cwd = workingDirectory,
                version = "0.1.0",
                gitBranch,
                status,
                messageText = message
            },
            cancellationToken);

        return systemUuid;
    }

    private async Task<DesktopSessionTurnResult> BuildCancelledTurnResultAsync(
        WorkspacePaths paths,
        string sessionId,
        string transcriptPath,
        string workingDirectory,
        string gitBranch,
        string promptOrTitle,
        NativeToolExecutionResult toolExecution,
        bool createdNewSession,
        ResolvedCommand? resolvedCommand,
        CancellationToken cancellationToken)
    {
        const string cancellationMessage = "Desktop turn cancelled before completion.";

        if (File.Exists(transcriptPath))
        {
            await transcriptWriter.AppendEntryAsync(
                transcriptPath,
                new
                {
                    uuid = Guid.NewGuid().ToString(),
                    parentUuid = transcriptWriter.TryReadLastEntryUuid(transcriptPath),
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
                                text = cancellationMessage
                            }
                        },
                        provider = "native-runtime",
                        model = "cancelled"
                    }
                },
                cancellationToken);
        }

        await RefreshSessionRecordingAsync(
            transcriptPath,
            new SessionRecordingContext
            {
                SessionId = sessionId,
                WorkingDirectory = workingDirectory,
                GitBranch = gitBranch,
                TitleHint = promptOrTitle,
                Status = "cancelled"
            },
            cancellationToken);

        PublishSessionEvent(sessionEventFactory.CreateTurnCancelled(
            sessionId,
            cancellationMessage,
            workingDirectory,
            gitBranch,
            resolvedCommand?.Name ?? string.Empty,
            toolExecution.ToolName));

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, sessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(sessionId, transcriptPath, workingDirectory, gitBranch, promptOrTitle, "cancelled");

        if (!string.Equals(session.Status, "cancelled", StringComparison.Ordinal))
        {
            session = new SessionPreview
            {
                SessionId = session.SessionId,
                Title = session.Title,
                LastActivity = session.LastActivity,
                Category = session.Category,
                Mode = session.Mode,
                Status = "cancelled",
                WorkingDirectory = session.WorkingDirectory,
                GitBranch = session.GitBranch,
                MessageCount = session.MessageCount,
                TranscriptPath = session.TranscriptPath,
                StartedAt = session.StartedAt,
                LastUpdatedAt = session.LastUpdatedAt,
                MetadataPath = session.MetadataPath
            };
        }

        await ExecuteNotificationHookAsync(
            runtimeProfileService.Inspect(paths),
            sessionId,
            workingDirectory,
            transcriptPath,
            "turn_cancelled",
            cancellationMessage,
            cancellationToken);
        await ExecuteSessionEndHookAsync(
            runtimeProfileService.Inspect(paths),
            sessionId,
            workingDirectory,
            transcriptPath,
            "cancelled",
            cancellationMessage,
            cancellationToken);

        return new DesktopSessionTurnResult
        {
            Session = session,
            AssistantSummary = cancellationMessage,
            CreatedNewSession = createdNewSession,
            ToolExecution = toolExecution,
            ResolvedCommand = resolvedCommand
        };
    }

    private static SessionPreview BuildFallbackSession(
        string sessionId,
        string transcriptPath,
        string workingDirectory,
        string gitBranch,
        string prompt,
        string status = "resume-ready") =>
        new()
        {
            SessionId = sessionId,
            Title = prompt.Length > 140 ? $"{prompt[..140]}..." : prompt,
            LastActivity = DateTime.UtcNow.ToString("O"),
            StartedAt = DateTime.UtcNow.ToString("O"),
            LastUpdatedAt = DateTime.UtcNow.ToString("O"),
            Category = string.IsNullOrWhiteSpace(gitBranch) ? "session" : gitBranch,
            Mode = DesktopMode.Code,
            Status = status,
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            MessageCount = 2,
            TranscriptPath = transcriptPath,
            MetadataPath = Path.Combine(
                Path.GetDirectoryName(transcriptPath) ?? string.Empty,
                $"{Path.GetFileNameWithoutExtension(transcriptPath)}.meta.json")
        };

    private Task<SessionRecordingMetadata?> RefreshSessionRecordingAsync(
        string transcriptPath,
        SessionRecordingContext context,
        CancellationToken cancellationToken) =>
        chatRecordingService.RefreshMetadataAsync(transcriptPath, context, cancellationToken);

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

    private static AssistantTurnRequest CreateAssistantTurnRequest(
        string sessionId,
        string prompt,
        string workingDirectory,
        string transcriptPath,
        QwenRuntimeProfile runtimeProfile,
        string gitBranch,
        CommandInvocationResult? commandInvocation,
        ResolvedCommand? resolvedCommand,
        NativeToolExecutionResult toolExecution,
        bool isApprovalResolution) =>
        new()
        {
            SessionId = sessionId,
            Prompt = prompt,
            WorkingDirectory = workingDirectory,
            TranscriptPath = transcriptPath,
            RuntimeProfile = runtimeProfile,
            GitBranch = gitBranch,
            CommandInvocation = commandInvocation,
            ResolvedCommand = resolvedCommand,
            ToolExecution = toolExecution,
            IsApprovalResolution = isApprovalResolution
        };

    private static ActiveTurnState CreateActiveTurnState(
        string sessionId,
        string prompt,
        string transcriptPath,
        string workingDirectory,
        string gitBranch,
        string toolName)
    {
        var timestampUtc = DateTime.UtcNow;
        return new ActiveTurnState
        {
            SessionId = sessionId,
            Prompt = prompt,
            TranscriptPath = transcriptPath,
            WorkingDirectory = workingDirectory,
            GitBranch = gitBranch,
            ToolName = toolName,
            Stage = "turn-started",
            Status = "started",
            ContentSnapshot = string.Empty,
            StartedAtUtc = timestampUtc,
            LastUpdatedAtUtc = timestampUtc
        };
    }

    private static string BuildRecoveryPrompt(InterruptedTurnState interruptedTurn, string recoveryNote)
    {
        var lines = new List<string>
        {
            interruptedTurn.Prompt,
            string.Empty,
            "The previous desktop turn was interrupted before completion.",
        };

        if (!string.IsNullOrWhiteSpace(interruptedTurn.ContentSnapshot))
        {
            lines.Add($"Last assistant snapshot: {interruptedTurn.ContentSnapshot}");
        }

        if (!string.IsNullOrWhiteSpace(interruptedTurn.ToolName))
        {
            lines.Add($"Last active tool: {interruptedTurn.ToolName}");
        }

        if (!string.IsNullOrWhiteSpace(recoveryNote))
        {
            lines.Add($"Recovery note: {recoveryNote.Trim()}");
        }

        lines.Add("Resume from this point and continue the coding session.");
        return string.Join(Environment.NewLine, lines);
    }

    private static void ApplyRuntimeEvent(ActiveTurnState state, AssistantRuntimeEvent runtimeEvent)
    {
        state.Stage = runtimeEvent.Stage;
        state.Status = string.IsNullOrWhiteSpace(runtimeEvent.Status) ? runtimeEvent.Stage : runtimeEvent.Status;

        if (!string.IsNullOrWhiteSpace(runtimeEvent.ToolName))
        {
            state.ToolName = runtimeEvent.ToolName;
        }

        if (!string.IsNullOrWhiteSpace(runtimeEvent.ContentSnapshot))
        {
            state.ContentSnapshot = runtimeEvent.ContentSnapshot;
        }
    }

    private void PublishSessionEvent(DesktopSessionEvent sessionEvent) =>
        SessionEvent?.Invoke(this, sessionEvent);

    private static NativeToolExecutionResult CreateNoToolExecutionResult(string workingDirectory) =>
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

    private static NativeToolExecutionResult CreateBlockedHookExecutionResult(string workingDirectory, string reason) =>
        new()
        {
            ToolName = "UserPromptSubmit",
            Status = "blocked",
            ApprovalState = "deny",
            WorkingDirectory = workingDirectory,
            Output = string.Empty,
            ErrorMessage = reason,
            ExitCode = 2,
            ChangedFiles = []
        };

    private static NativeToolExecutionResult CreateCancelledToolExecutionResult(string workingDirectory) =>
        new()
        {
            ToolName = string.Empty,
            Status = "cancelled",
            ApprovalState = "cancelled",
            WorkingDirectory = workingDirectory,
            Output = string.Empty,
            ErrorMessage = "Turn cancelled by user.",
            ExitCode = -1,
            ChangedFiles = []
        };

    private static UserPromptHookResult CreatePromptHookResult(string prompt, HookLifecycleResult hookResult) =>
        new()
        {
            EffectivePrompt = prompt,
            AdditionalContext = hookResult.AggregateOutput.AdditionalContext,
            SystemMessage = hookResult.AggregateOutput.SystemMessage,
            IsBlocked = hookResult.IsBlocked,
            BlockReason = ResolveHookBlockReason(hookResult),
            Executions = hookResult.Executions
        };

    private static string ApplyStopHookSummary(string assistantSummary, HookLifecycleResult stopHook)
    {
        if (!stopHook.IsBlocked && stopHook.AggregateOutput.Continue != false)
        {
            return assistantSummary;
        }

        return !string.IsNullOrWhiteSpace(stopHook.AggregateOutput.StopReason)
            ? stopHook.AggregateOutput.StopReason
            : ResolveHookBlockReason(stopHook);
    }

    private async Task<HookLifecycleResult> ExecuteLifecycleHookAsync(
        QwenRuntimeProfile runtimeProfile,
        HookEventName eventName,
        string sessionId,
        string workingDirectory,
        string transcriptPath,
        string prompt = "",
        string toolName = "",
        string toolStatus = "",
        string approvalState = "",
        string toolOutput = "",
        string reason = "",
        string agentName = "",
        JsonObject? metadata = null,
        CancellationToken cancellationToken = default) =>
        await hookLifecycleService.ExecuteAsync(
            runtimeProfile,
            new HookInvocationRequest
            {
                EventName = eventName,
                SessionId = sessionId,
                WorkingDirectory = workingDirectory,
                TranscriptPath = transcriptPath,
                Prompt = prompt,
                ToolName = toolName,
                ToolStatus = toolStatus,
                ApprovalState = approvalState,
                ToolOutput = toolOutput,
                Reason = reason,
                AgentName = agentName,
                Metadata = metadata ?? []
            },
            cancellationToken);

    private async Task ExecuteSessionEndHookAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        string workingDirectory,
        string transcriptPath,
        string trigger,
        string message,
        CancellationToken cancellationToken)
    {
        _ = await ExecuteLifecycleHookAsync(
            runtimeProfile,
            HookEventName.SessionEnd,
            sessionId,
            workingDirectory,
            transcriptPath,
            reason: message,
            metadata: new JsonObject
            {
                ["trigger"] = trigger,
                ["reason"] = trigger
            },
            cancellationToken: cancellationToken);
    }

    private async Task ExecuteNotificationHookAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        string workingDirectory,
        string transcriptPath,
        string notificationType,
        string message,
        CancellationToken cancellationToken)
    {
        _ = await ExecuteLifecycleHookAsync(
            runtimeProfile,
            HookEventName.Notification,
            sessionId,
            workingDirectory,
            transcriptPath,
            reason: message,
            metadata: new JsonObject
            {
                ["notification_type"] = notificationType,
                ["message"] = message
            },
            cancellationToken: cancellationToken);
    }

    private static string ResolveHookBlockReason(HookLifecycleResult hookResult)
    {
        if (!string.IsNullOrWhiteSpace(hookResult.BlockReason))
        {
            return hookResult.BlockReason;
        }

        if (!string.IsNullOrWhiteSpace(hookResult.AggregateOutput.StopReason))
        {
            return hookResult.AggregateOutput.StopReason;
        }

        if (!string.IsNullOrWhiteSpace(hookResult.AggregateOutput.Reason))
        {
            return hookResult.AggregateOutput.Reason;
        }

        return "Execution was blocked by a configured hook.";
    }

}
