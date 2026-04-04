using System.Diagnostics;
using Microsoft.Extensions.Options;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

public sealed class AssistantTurnRuntime(
    IAssistantPromptAssembler promptAssembler,
    IEnumerable<IAssistantResponseProvider> providers,
    IToolExecutor toolExecutor,
    ILoopDetectionService loopDetectionService,
    IOptions<NativeAssistantRuntimeOptions> options) : IAssistantTurnRuntime
{
    private readonly IReadOnlyList<IAssistantResponseProvider> _providers = providers.ToArray();
    private readonly NativeAssistantRuntimeOptions _options = options.Value;

    public async Task<AssistantTurnResponse> GenerateAsync(
        AssistantTurnRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        loopDetectionService.Reset(request.SessionId);

        try
        {
        eventSink?.Invoke(new AssistantRuntimeEvent
        {
            Stage = "assembling-context",
            Message = "Assembling transcript and workspace context for the assistant runtime."
        });

        var promptContext = await promptAssembler.AssembleAsync(request, cancellationToken);
        var toolHistory = new List<AssistantToolCallResult>();

        foreach (var provider in BuildProviderChain())
        {
            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "generating",
                ProviderName = provider.Name,
                Message = $"Generating assistant response via {provider.Name}."
            });

            try
            {
                var response = await RunProviderLoopAsync(
                    provider,
                    request,
                    promptContext,
                    toolHistory,
                    eventSink,
                    cancellationToken);
                if (response is not null)
                {
                    return FinalizeResponse(response, startedAtUtc, stopwatch.ElapsedMilliseconds);
                }
            }
            catch
            {
                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "fallback",
                    ProviderName = provider.Name,
                    Message = $"Provider {provider.Name} failed. Falling back to the local assistant runtime."
                });
            }
        }

        throw new InvalidOperationException("No assistant runtime provider returned a response.");
        }
        finally
        {
            loopDetectionService.Complete(request.SessionId);
        }
    }

    private IReadOnlyList<IAssistantResponseProvider> BuildProviderChain()
    {
        var resolvedProviders = new List<IAssistantResponseProvider>();
        var preferredProvider = _providers.FirstOrDefault(provider =>
            string.Equals(_options.Provider, provider.Name, StringComparison.OrdinalIgnoreCase));
        if (preferredProvider is not null)
        {
            resolvedProviders.Add(preferredProvider);
        }

        var fallbackProvider = _providers.FirstOrDefault(static provider =>
            string.Equals(provider.Name, "fallback", StringComparison.OrdinalIgnoreCase));
        if (fallbackProvider is not null && !resolvedProviders.Contains(fallbackProvider))
        {
            resolvedProviders.Add(fallbackProvider);
        }

        return resolvedProviders;
    }

    private async Task<AssistantTurnResponse?> RunProviderLoopAsync(
        IAssistantResponseProvider provider,
        AssistantTurnRequest request,
        AssistantPromptContext promptContext,
        List<AssistantToolCallResult> toolHistory,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken)
    {
        LoopDetectionDecision? detectedContentLoop = null;
        var maxIterations = Math.Max(1, _options.MaxToolIterations);
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var roundCount = iteration + 1;
            var response = await provider.TryGenerateAsync(
                request,
                promptContext,
                toolHistory,
                _options,
                runtimeEvent =>
                {
                    if (detectedContentLoop is null &&
                        !string.IsNullOrWhiteSpace(runtimeEvent.ContentDelta))
                    {
                        var decision = loopDetectionService.ObserveContentDelta(request.SessionId, runtimeEvent.ContentDelta);
                        if (decision.IsDetected)
                        {
                            detectedContentLoop = decision;
                        }
                    }

                    eventSink?.Invoke(runtimeEvent);
                },
                cancellationToken);
            if (response is null)
            {
                return null;
            }

            if (detectedContentLoop is { IsDetected: true })
            {
                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "loop-detected",
                    ProviderName = provider.Name,
                    Status = "blocked",
                    Message = detectedContentLoop.Reason
                });

                return new AssistantTurnResponse
                {
                    Summary = $"Assistant runtime stopped because loop detection found a repeated content pattern.",
                    ProviderName = provider.Name,
                    Model = response.Model,
                    StopReason = "content-loop-detected",
                    ToolExecutions = toolHistory.ToArray(),
                    Stats = AssistantExecutionDiagnostics.BuildStats(roundCount, toolHistory)
                };
            }

            if (response.ToolCalls.Count == 0)
            {
                return new AssistantTurnResponse
                {
                    Summary = response.Summary,
                    ProviderName = response.ProviderName,
                    Model = response.Model,
                    StopReason = AssistantExecutionDiagnostics.ResolveStopReason(response),
                    ToolCalls = response.ToolCalls,
                    ToolExecutions = toolHistory.ToArray(),
                    Stats = AssistantExecutionDiagnostics.BuildStats(roundCount, toolHistory)
                };
            }

            foreach (var toolCall in response.ToolCalls)
            {
                var loopDecision = loopDetectionService.ObserveToolCall(request.SessionId, toolCall);
                if (loopDecision.IsDetected)
                {
                    eventSink?.Invoke(new AssistantRuntimeEvent
                    {
                        Stage = "loop-detected",
                        ProviderName = provider.Name,
                        ToolName = toolCall.ToolName,
                        Status = "blocked",
                        Message = loopDecision.Reason
                    });

                    return new AssistantTurnResponse
                    {
                        Summary = $"Assistant runtime stopped because loop detection found repeated tool calls for '{toolCall.ToolName}'.",
                        ProviderName = provider.Name,
                        Model = response.Model,
                        StopReason = "tool-loop-detected",
                        ToolExecutions = toolHistory.ToArray(),
                        Stats = AssistantExecutionDiagnostics.BuildStats(roundCount, toolHistory)
                    };
                }

                if (request.AllowedToolNames.Count > 0 &&
                    !request.AllowedToolNames.Contains(toolCall.ToolName, StringComparer.OrdinalIgnoreCase))
                {
                    var deniedExecution = CreateDisallowedToolExecutionResult(toolCall.ToolName, request.RuntimeProfile.ProjectRoot);
                    toolHistory.Add(new AssistantToolCallResult
                    {
                        ToolCall = toolCall,
                        Execution = deniedExecution
                    });

                    eventSink?.Invoke(new AssistantRuntimeEvent
                    {
                        Stage = "tool-blocked",
                        ProviderName = provider.Name,
                        ToolName = toolCall.ToolName,
                        Status = deniedExecution.Status,
                        Message = BuildToolMessage(deniedExecution)
                    });
                    break;
                }

                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = "tool-requested",
                    ProviderName = provider.Name,
                    ToolName = toolCall.ToolName,
                    Status = "requested",
                    Message = $"Assistant runtime requested native tool '{toolCall.ToolName}'."
                });

                var execution = await toolExecutor.ExecuteAsync(
                    new WorkspacePaths { WorkspaceRoot = request.RuntimeProfile.ProjectRoot },
                    new ExecuteNativeToolRequest
                    {
                        ToolName = toolCall.ToolName,
                        ArgumentsJson = string.IsNullOrWhiteSpace(toolCall.ArgumentsJson) ? "{}" : toolCall.ArgumentsJson,
                        ApproveExecution = false
                    },
                    toolEvent => eventSink?.Invoke(CloneNestedToolEvent(toolEvent, provider.Name)),
                    cancellationToken);

                var toolResult = new AssistantToolCallResult
                {
                    ToolCall = toolCall,
                    Execution = execution
                };
                toolHistory.Add(toolResult);

                eventSink?.Invoke(new AssistantRuntimeEvent
                {
                    Stage = MapToolStage(execution.Status),
                    ProviderName = provider.Name,
                    ToolName = execution.ToolName,
                    Status = execution.Status,
                    Message = BuildToolMessage(execution)
                });

                if (!string.Equals(execution.Status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        var fallbackSummary = toolHistory.Count == 0
            ? "Assistant runtime stopped before producing a final response."
            : $"Assistant runtime reached the tool iteration limit after {toolHistory.Count} native tool execution(s).";

        return new AssistantTurnResponse
        {
            Summary = fallbackSummary,
            ProviderName = provider.Name,
            Model = string.IsNullOrWhiteSpace(request.ModelOverride) ? _options.Model : request.ModelOverride,
            StopReason = "iteration-limit",
            ToolExecutions = toolHistory.ToArray(),
            Stats = AssistantExecutionDiagnostics.BuildStats(maxIterations, toolHistory)
        };
    }

    private static string MapToolStage(string status) =>
        status switch
        {
            "approval-required" => "tool-approval-required",
            "input-required" => "user-input-required",
            "blocked" => "tool-blocked",
            "error" => "tool-failed",
            _ => "tool-completed"
        };

    private static string BuildToolMessage(NativeToolExecutionResult execution) =>
        execution.Status switch
        {
            "approval-required" => $"Assistant runtime requested native tool '{execution.ToolName}', and it is waiting for approval.",
            "input-required" => $"Assistant runtime requested native tool '{execution.ToolName}', and it is waiting for user answers.",
            "blocked" => $"Assistant runtime requested native tool '{execution.ToolName}', but approval policy blocked it.",
            "error" => $"Assistant runtime requested native tool '{execution.ToolName}', but execution failed: {execution.ErrorMessage}",
            _ => $"Assistant runtime completed native tool '{execution.ToolName}'."
        };

    private static NativeToolExecutionResult CreateDisallowedToolExecutionResult(string toolName, string workingDirectory) =>
        new()
        {
            ToolName = toolName,
            Status = "blocked",
            ApprovalState = "deny",
            WorkingDirectory = workingDirectory,
            ErrorMessage = $"Tool '{toolName}' is not available to this subagent runtime.",
            ChangedFiles = []
        };

    private static AssistantRuntimeEvent CloneNestedToolEvent(AssistantRuntimeEvent runtimeEvent, string providerName) =>
        new()
        {
            Stage = runtimeEvent.Stage,
            Message = runtimeEvent.Message,
            ProviderName = string.IsNullOrWhiteSpace(runtimeEvent.ProviderName) ? providerName : runtimeEvent.ProviderName,
            ToolName = runtimeEvent.ToolName,
            Status = runtimeEvent.Status,
            ContentDelta = runtimeEvent.ContentDelta,
            ContentSnapshot = runtimeEvent.ContentSnapshot,
            AgentName = runtimeEvent.AgentName
        };

    private static AssistantTurnResponse FinalizeResponse(
        AssistantTurnResponse response,
        DateTime startedAtUtc,
        long durationMs)
    {
        var endedAtUtc = startedAtUtc.AddMilliseconds(Math.Max(0, durationMs));
        var resolvedStats = AssistantExecutionDiagnostics.ResolveStats(response, startedAtUtc, endedAtUtc);
        return new AssistantTurnResponse
        {
            Summary = response.Summary,
            ProviderName = response.ProviderName,
            Model = response.Model,
            StopReason = AssistantExecutionDiagnostics.ResolveStopReason(response),
            Stats = new AssistantExecutionStats
            {
                RoundCount = resolvedStats.RoundCount,
                ToolCallCount = resolvedStats.ToolCallCount,
                SuccessfulToolCallCount = resolvedStats.SuccessfulToolCallCount,
                FailedToolCallCount = resolvedStats.FailedToolCallCount,
                DurationMs = Math.Max(resolvedStats.DurationMs, durationMs)
            },
            ToolCalls = response.ToolCalls,
            ToolExecutions = response.ToolExecutions
        };
    }
}
