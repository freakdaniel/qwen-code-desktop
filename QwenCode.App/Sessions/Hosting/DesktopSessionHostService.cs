using QwenCode.App.Models;
using QwenCode.App.Compatibility;
using QwenCode.App.Runtime;
using QwenCode.App.Tools;

namespace QwenCode.App.Sessions;

public sealed class DesktopSessionHostService(
    QwenRuntimeProfileService runtimeProfileService,
    ICommandActionRuntime commandActionRuntime,
    IAssistantTurnRuntime assistantTurnRuntime,
    IToolExecutor nativeToolHostService,
    IUserQuestionToolService userQuestionToolService,
    ITranscriptStore sessionCatalogService,
    IActiveTurnRegistry activeTurnRegistry,
    IInterruptedTurnStore interruptedTurnStore,
    ISessionTranscriptWriter transcriptWriter,
    ISessionEventFactory sessionEventFactory,
    IPendingApprovalResolver pendingApprovalResolver) : ISessionHost
{
    public event EventHandler<DesktopSessionEvent>? SessionEvent;

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

        PublishSessionEvent(sessionEventFactory.CreateTurnStarted(
            sessionId,
            request.Prompt,
            workingDirectory,
            gitBranch,
            request.ToolName ?? string.Empty));

        Directory.CreateDirectory(runtimeProfile.ChatsDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var commandInvocation = await commandActionRuntime.TryInvokeAsync(paths, request.Prompt, workingDirectory, cancellationToken);
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
                cancellationToken);

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
                request.Prompt,
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
        var assistantSummary = assistantResponse.Summary;
        var assistantTimestamp = DateTime.UtcNow;
        parentUuid = await transcriptWriter.AppendAssistantToolExecutionsAsync(
            transcriptPath,
            sessionId,
            parentUuid,
            gitBranch,
            assistantResponse.ToolExecutions,
            cancellationToken);
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

        var session = sessionCatalogService.ListSessions(paths, 64)
            .FirstOrDefault(item => string.Equals(item.SessionId, sessionId, StringComparison.Ordinal))
            ?? BuildFallbackSession(sessionId, transcriptPath, workingDirectory, gitBranch, request.Prompt);

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

        return result;
    }

    public async Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
        WorkspacePaths paths,
        ApproveDesktopSessionToolRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to approve a pending tool.");
        }

        var detail = sessionCatalogService.GetSession(paths, new GetDesktopSessionRequest
        {
            SessionId = request.SessionId
        })
            ?? throw new InvalidOperationException("Session transcript was not found.");
        var workingDirectory = string.IsNullOrWhiteSpace(detail.Session.WorkingDirectory)
            ? runtimeProfileService.Inspect(paths).ProjectRoot
            : detail.Session.WorkingDirectory;
        var gitBranch = detail.Session.GitBranch;

        return await activeTurnRegistry.RunAsync(
            request.SessionId,
            CreateActiveTurnState(
                request.SessionId,
                detail.Session.Title,
                detail.TranscriptPath,
                workingDirectory,
                gitBranch,
                string.Empty),
            token => ApprovePendingToolCoreAsync(paths, request, token),
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

    public async Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
        WorkspacePaths paths,
        AnswerDesktopSessionQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to answer a pending question.");
        }

        var detail = sessionCatalogService.GetSession(paths, new GetDesktopSessionRequest
        {
            SessionId = request.SessionId
        })
            ?? throw new InvalidOperationException("Session transcript was not found.");
        var workingDirectory = string.IsNullOrWhiteSpace(detail.Session.WorkingDirectory)
            ? runtimeProfileService.Inspect(paths).ProjectRoot
            : detail.Session.WorkingDirectory;
        var gitBranch = detail.Session.GitBranch;

        return await activeTurnRegistry.RunAsync(
            request.SessionId,
            CreateActiveTurnState(
                request.SessionId,
                detail.Session.Title,
                detail.TranscriptPath,
                workingDirectory,
                gitBranch,
                "ask_user_question"),
            token => AnswerPendingQuestionCoreAsync(paths, request, token),
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
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to approve a pending tool.");
        }

        var detail = sessionCatalogService.GetSession(paths, new GetDesktopSessionRequest
        {
            SessionId = request.SessionId
        })
            ?? throw new InvalidOperationException("Session transcript was not found.");
        var pendingTool = pendingApprovalResolver.ResolvePendingTool(detail, request.EntryId);

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
        await transcriptWriter.MarkToolEntryResolvedAsync(
            detail.TranscriptPath,
            pendingTool.Id,
            "approved",
            resolutionTimestamp,
            cancellationToken);

        var parentUuid = transcriptWriter.TryReadLastEntryUuid(detail.TranscriptPath);
        var gitBranch = pendingTool.GitBranch;
        var workingDirectory = string.IsNullOrWhiteSpace(pendingTool.WorkingDirectory)
            ? runtimeProfile.ProjectRoot
            : pendingTool.WorkingDirectory;

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

        return result;
    }

    private async Task<DesktopSessionTurnResult> AnswerPendingQuestionCoreAsync(
        WorkspacePaths paths,
        AnswerDesktopSessionQuestionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("SessionId is required to answer a pending question.");
        }

        var detail = sessionCatalogService.GetSession(paths, new GetDesktopSessionRequest
        {
            SessionId = request.SessionId
        })
            ?? throw new InvalidOperationException("Session transcript was not found.");
        var pendingQuestion = pendingApprovalResolver.ResolvePendingQuestion(detail, request.EntryId);
        var questions = pendingQuestion.Questions.Count > 0
            ? pendingQuestion.Questions
            : userQuestionToolService.ParseQuestions(pendingQuestion.Arguments);
        var answers = userQuestionToolService.ValidateAnswers(questions, request.Answers);

        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var workingDirectory = string.IsNullOrWhiteSpace(pendingQuestion.WorkingDirectory)
            ? runtimeProfile.ProjectRoot
            : pendingQuestion.WorkingDirectory;
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
        var gitBranch = pendingQuestion.GitBranch;

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

        return result;
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
                TranscriptPath = session.TranscriptPath
            };
        }

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
            LastActivity = "Updated just now",
            Category = string.IsNullOrWhiteSpace(gitBranch) ? "session" : gitBranch,
            Mode = DesktopMode.Code,
            Status = status,
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

}
