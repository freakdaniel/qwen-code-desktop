using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QwenCode.App.Compatibility;
using QwenCode.App.Hooks;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Runtime;
using QwenCode.App.Tools;

namespace QwenCode.App.Agents;

public sealed class SubagentCoordinatorService(
    ISubagentCatalog subagentCatalog,
    IToolRegistry toolRegistry,
    QwenCompatibilityService compatibilityService,
    IHookLifecycleService? hookLifecycleService = null,
    IServiceProvider? serviceProvider = null) : ISubagentCoordinator
{
    public async Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        var description = TryGetRequiredString(arguments, "description");
        var prompt = TryGetRequiredString(arguments, "prompt");
        var subagentType = TryGetRequiredString(arguments, "subagent_type");

        if (string.IsNullOrWhiteSpace(description))
        {
            return Error("Parameter 'description' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Error("Parameter 'prompt' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (string.IsNullOrWhiteSpace(subagentType))
        {
            return Error("Parameter 'subagent_type' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        var agent = subagentCatalog.FindAgent(paths, subagentType);
        if (agent is null)
        {
            var availableAgents = string.Join(", ", subagentCatalog.ListAgents(paths).Select(static item => item.Name));
            return Error($"Subagent '{subagentType}' not found. Available subagents: {availableAgents}", runtimeProfile.ProjectRoot, approvalState);
        }

        var executionId = $"agent-{Guid.NewGuid():N}";
        var timestampUtc = DateTime.UtcNow;
        var executionDirectory = Path.Combine(runtimeProfile.RuntimeBaseDirectory, "agents");
        Directory.CreateDirectory(executionDirectory);

        var transcriptPath = Path.Combine(executionDirectory, $"{executionId}.jsonl");
        var artifactPath = Path.Combine(executionDirectory, $"{executionId}.json");
        var startHook = await ExecuteHookAsync(
            runtimeProfile,
            HookEventName.SubagentStart,
            executionId,
            transcriptPath,
            description,
            prompt,
            agent.Name,
            "started",
            cancellationToken);
        if (startHook.IsBlocked)
        {
            return new NativeToolExecutionResult
            {
                ToolName = "agent",
                Status = "blocked",
                ApprovalState = approvalState,
                WorkingDirectory = runtimeProfile.ProjectRoot,
                ErrorMessage = startHook.BlockReason,
                ChangedFiles = []
            };
        }

        var effectivePrompt = ApplyHookPrompt(startHook.AggregateOutput, prompt);
        var runtimeRequest = BuildAssistantTurnRequest(executionId, description, effectivePrompt, agent, runtimeProfile, transcriptPath);
        var runtime = ResolveRuntime();
        eventSink?.Invoke(CreateSubagentRuntimeEvent(
            "generating",
            agent.Name,
            "started",
            "agent",
            $"Subagent '{agent.Name}' is starting."));
        var response = await runtime.GenerateAsync(
            runtimeRequest,
            runtimeEvent => eventSink?.Invoke(ForwardSubagentRuntimeEvent(agent.Name, runtimeEvent)),
            cancellationToken);
        var stopHook = await ExecuteHookAsync(
            runtimeProfile,
            HookEventName.SubagentStop,
            executionId,
            transcriptPath,
            description,
            effectivePrompt,
            agent.Name,
            ResolveOverallStatus(response),
            cancellationToken,
            response.Summary);
        eventSink?.Invoke(CreateSubagentRuntimeEvent(
            MapCompletionStage(response),
            agent.Name,
            ResolveOverallStatus(response),
            "agent",
            $"Subagent '{agent.Name}' finished with status '{ResolveOverallStatus(response)}'."));

        await PersistTranscriptAsync(
            transcriptPath,
            executionId,
            runtimeProfile.ProjectRoot,
            runtimeRequest.Prompt,
            response,
            cancellationToken);

        var report = BuildReport(agent, description, effectivePrompt, runtimeProfile, response, stopHook.AggregateOutput);
        var status = ResolveOverallStatus(response);
        var reportApprovalState = ResolveApprovalState(response, approvalState);
        var record = new SubagentExecutionRecord
        {
            ExecutionId = executionId,
            AgentName = agent.Name,
            Description = description,
            Prompt = prompt,
            Scope = agent.Scope,
            FilePath = agent.FilePath,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Status = status,
            Report = report,
            ProviderName = response.ProviderName,
            Model = response.Model,
            TranscriptPath = transcriptPath,
            AllowedTools = agent.Tools,
            ToolExecutions = response.ToolExecutions
                .Select(static item => new SubagentToolExecutionRecord
                {
                    ToolName = item.Execution.ToolName,
                    Status = item.Execution.Status,
                    ApprovalState = item.Execution.ApprovalState,
                    ErrorMessage = item.Execution.ErrorMessage,
                    ChangedFiles = item.Execution.ChangedFiles
                })
                .ToArray(),
            TimestampUtc = timestampUtc
        };
        await File.WriteAllTextAsync(
            artifactPath,
            JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        return new NativeToolExecutionResult
        {
            ToolName = "agent",
            Status = status,
            ApprovalState = reportApprovalState,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Output = report,
            ErrorMessage = ResolveErrorMessage(response),
            ChangedFiles = [artifactPath, transcriptPath]
        };
    }

    private async Task<HookLifecycleResult> ExecuteHookAsync(
        QwenRuntimeProfile runtimeProfile,
        HookEventName eventName,
        string executionId,
        string transcriptPath,
        string description,
        string prompt,
        string agentName,
        string status,
        CancellationToken cancellationToken,
        string toolOutput = "")
    {
        if (hookLifecycleService is null)
        {
            return new HookLifecycleResult();
        }

        return await hookLifecycleService.ExecuteAsync(
            runtimeProfile,
            new HookInvocationRequest
            {
                EventName = eventName,
                SessionId = executionId,
                WorkingDirectory = runtimeProfile.ProjectRoot,
                TranscriptPath = transcriptPath,
                Prompt = prompt,
                AgentName = agentName,
                ToolStatus = status,
                ToolOutput = toolOutput,
                Metadata = new JsonObject
                {
                    ["description"] = description,
                    ["scope"] = runtimeProfile.ProjectRoot
                }
            },
            cancellationToken);
    }

    private static string ApplyHookPrompt(HookOutput output, string prompt) =>
        string.IsNullOrWhiteSpace(output.ModifiedPrompt) ? prompt : output.ModifiedPrompt;

    private IAssistantTurnRuntime ResolveRuntime()
    {
        if (serviceProvider?.GetService<IAssistantTurnRuntime>() is { } runtime)
        {
            return runtime;
        }

        return new AssistantTurnRuntime(
            new AssistantPromptAssembler(new ProjectSummaryService()),
            [new FallbackAssistantResponseProvider()],
            new ToolCallScheduler(
                new NonInteractiveToolExecutor(new NoOpToolExecutor()),
                new LoopDetectionService()),
            new LoopDetectionService(),
            new TokenLimitService(),
            new ProviderConfigurationResolver(new DesktopEnvironmentPaths()),
            Microsoft.Extensions.Options.Options.Create(new NativeAssistantRuntimeOptions
            {
                Provider = "fallback"
            }));
    }

    private static AssistantTurnRequest BuildAssistantTurnRequest(
        string executionId,
        string description,
        string prompt,
        SubagentDescriptor agent,
        QwenRuntimeProfile runtimeProfile,
        string transcriptPath) =>
        new()
        {
            SessionId = executionId,
            Prompt = BuildDelegatedPrompt(description, prompt, agent),
            WorkingDirectory = runtimeProfile.ProjectRoot,
            TranscriptPath = transcriptPath,
            RuntimeProfile = runtimeProfile,
            GitBranch = string.Empty,
            ToolExecution = new NativeToolExecutionResult
            {
                ToolName = "not-requested",
                Status = "not-requested",
                ApprovalState = "not-requested",
                WorkingDirectory = runtimeProfile.ProjectRoot,
                ChangedFiles = []
            },
            SystemPromptOverride = BuildSystemPrompt(agent),
            AllowedToolNames = agent.Tools
        };

    private static string BuildDelegatedPrompt(string description, string prompt, SubagentDescriptor agent) =>
        $$"""
Task description: {{description}}
Subagent type: {{agent.Name}}
Subagent scope: {{agent.Scope}}

Parent request:
{{prompt.Trim()}}

Return only the information the parent runtime needs to continue the task.
Be concise, evidence-driven, and explicit about blockers, approvals, and changed files.
""";

    private static string BuildSystemPrompt(SubagentDescriptor agent)
    {
        var allowedTools = agent.Tools.Count == 0
            ? "inherit the native desktop tool surface"
            : string.Join(", ", agent.Tools);

        return $$"""
You are a specialized headless subagent running inside the native Qwen Code Desktop runtime.
Role: {{agent.Name}}
Description: {{agent.Description}}
Source: {{agent.FilePath}}

Specialized instructions:
{{agent.SystemPrompt}}

Operational rules:
- Work autonomously inside the delegated task scope.
- Use only the tools available to this subagent: {{allowedTools}}.
- Do not address the end user directly.
- Return a concise execution summary for the parent runtime.
""";
    }

    private async Task PersistTranscriptAsync(
        string transcriptPath,
        string executionId,
        string workingDirectory,
        string delegatedPrompt,
        AssistantTurnResponse response,
        CancellationToken cancellationToken)
    {
        var entries = new List<object>
        {
            new
            {
                sessionId = executionId,
                timestamp = DateTime.UtcNow,
                type = "user",
                cwd = workingDirectory,
                message = new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = delegatedPrompt
                        }
                    }
                }
            }
        };

        foreach (var tool in response.ToolExecutions)
        {
            entries.Add(new
            {
                sessionId = executionId,
                timestamp = DateTime.UtcNow,
                type = "tool",
                cwd = tool.Execution.WorkingDirectory,
                toolName = tool.Execution.ToolName,
                status = tool.Execution.Status,
                approvalState = tool.Execution.ApprovalState,
                output = tool.Execution.Output,
                errorMessage = tool.Execution.ErrorMessage
            });
        }

        entries.Add(new
        {
            sessionId = executionId,
            timestamp = DateTime.UtcNow,
            type = "assistant",
            cwd = workingDirectory,
            provider = response.ProviderName,
            model = response.Model,
            message = new
            {
                role = "assistant",
                parts = new[]
                {
                    new
                    {
                        text = response.Summary
                    }
                }
            }
        });

        await using var stream = new FileStream(transcriptPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        foreach (var entry in entries)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(entry));
        }
    }

    private string BuildReport(
        SubagentDescriptor agent,
        string description,
        string prompt,
        QwenRuntimeProfile runtimeProfile,
        AssistantTurnResponse response,
        HookOutput stopHookOutput)
    {
        var compatibility = compatibilityService.Inspect(new WorkspacePaths { WorkspaceRoot = runtimeProfile.ProjectRoot });
        var toolNames = toolRegistry.Inspect(new WorkspacePaths { WorkspaceRoot = runtimeProfile.ProjectRoot }).Tools
            .Select(static tool => tool.Name)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine($"Subagent '{agent.Name}' finished with status '{ResolveOverallStatus(response)}'.");
        builder.AppendLine();
        builder.AppendLine($"Description: {description}");
        builder.AppendLine($"Scope: {agent.Scope}");
        builder.AppendLine($"Source: {agent.FilePath}");
        builder.AppendLine($"Workspace: {runtimeProfile.ProjectRoot}");
        builder.AppendLine($"Provider: {response.ProviderName}");
        builder.AppendLine($"Model: {response.Model}");
        builder.AppendLine();
        builder.AppendLine("Delegated prompt:");
        builder.AppendLine(prompt.Trim());
        builder.AppendLine();
        builder.AppendLine("Assistant summary:");
        builder.AppendLine(string.IsNullOrWhiteSpace(response.Summary) ? "(empty response)" : response.Summary.Trim());
        builder.AppendLine();
        builder.AppendLine("Runtime context:");
        builder.AppendLine($"- Slash commands discovered: {compatibility.Commands.Count}");
        builder.AppendLine($"- Skills discovered: {compatibility.Skills.Count}");
        builder.AppendLine($"- Native tools available in host: {toolNames.Length}");
        builder.AppendLine($"- Allowed tools for this subagent: {(agent.Tools.Count == 0 ? "all available tools" : string.Join(", ", agent.Tools))}");

        if (response.ToolExecutions.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Subagent tool executions:");
            foreach (var toolExecution in response.ToolExecutions)
            {
                builder.AppendLine($"- {toolExecution.Execution.ToolName}: {toolExecution.Execution.Status}");
                if (!string.IsNullOrWhiteSpace(toolExecution.Execution.ErrorMessage))
                {
                    builder.AppendLine($"  error: {toolExecution.Execution.ErrorMessage}");
                }
            }
        }

        var hookNote = BuildHookNote(stopHookOutput);
        if (!string.IsNullOrWhiteSpace(hookNote))
        {
            builder.AppendLine();
            builder.AppendLine("Hook notes:");
            builder.AppendLine(hookNote);
        }

        return builder.ToString().Trim();
    }

    private static string BuildHookNote(HookOutput output)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(output.SystemMessage))
        {
            lines.Add(output.SystemMessage);
        }

        if (!string.IsNullOrWhiteSpace(output.AdditionalContext))
        {
            lines.Add(output.AdditionalContext);
        }

        return string.Join(Environment.NewLine, lines.Where(static value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string ResolveOverallStatus(AssistantTurnResponse response)
    {
        var toolStatus = response.ToolExecutions.LastOrDefault()?.Execution.Status;
        return string.IsNullOrWhiteSpace(toolStatus) || string.Equals(toolStatus, "completed", StringComparison.OrdinalIgnoreCase)
            ? "completed"
            : toolStatus;
    }

    private static string ResolveApprovalState(AssistantTurnResponse response, string approvalState) =>
        response.ToolExecutions.LastOrDefault()?.Execution.ApprovalState is { Length: > 0 } nestedApproval
            ? nestedApproval
            : approvalState;

    private static string ResolveErrorMessage(AssistantTurnResponse response) =>
        response.ToolExecutions.LastOrDefault(static item =>
                !string.Equals(item.Execution.Status, "completed", StringComparison.OrdinalIgnoreCase))
            ?.Execution.ErrorMessage ?? string.Empty;

    private static AssistantRuntimeEvent ForwardSubagentRuntimeEvent(string agentName, AssistantRuntimeEvent runtimeEvent) =>
        new()
        {
            Stage = runtimeEvent.Stage,
            Message = $"Subagent '{agentName}': {runtimeEvent.Message}",
            ProviderName = runtimeEvent.ProviderName,
            ToolName = runtimeEvent.ToolName,
            Status = runtimeEvent.Status,
            ContentDelta = runtimeEvent.ContentDelta,
            ContentSnapshot = runtimeEvent.ContentSnapshot,
            AgentName = agentName
        };

    private static AssistantRuntimeEvent CreateSubagentRuntimeEvent(
        string stage,
        string agentName,
        string status,
        string toolName,
        string message) =>
        new()
        {
            Stage = stage,
            Message = message,
            ToolName = toolName,
            Status = status,
            AgentName = agentName
        };

    private static string MapCompletionStage(AssistantTurnResponse response) =>
        ResolveOverallStatus(response) switch
        {
            "approval-required" => "tool-approval-required",
            "input-required" => "user-input-required",
            "blocked" => "tool-blocked",
            "error" => "tool-failed",
            _ => "tool-completed"
        };

    private static string? TryGetRequiredString(JsonElement arguments, string propertyName) =>
        arguments.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static NativeToolExecutionResult Error(string message, string workingDirectory, string approvalState) =>
        new()
        {
            ToolName = "agent",
            Status = "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            ErrorMessage = message,
            ChangedFiles = []
        };

    private sealed class NoOpToolExecutor : IToolExecutor
    {
        public NativeToolHostSnapshot Inspect(WorkspacePaths paths) =>
            new()
            {
                RegisteredCount = 0,
                ImplementedCount = 0,
                ReadyCount = 0,
                ApprovalRequiredCount = 0,
                Tools = []
            };

        public Task<NativeToolExecutionResult> ExecuteAsync(
            WorkspacePaths paths,
            ExecuteNativeToolRequest request,
            Action<AssistantRuntimeEvent>? eventSink = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new NativeToolExecutionResult
            {
                ToolName = request.ToolName,
                Status = "blocked",
                ApprovalState = "deny",
                WorkingDirectory = paths.WorkspaceRoot,
                ErrorMessage = "No tool executor is available for this fallback subagent runtime.",
                ChangedFiles = []
            });
    }
}
