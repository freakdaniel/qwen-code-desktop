using System.Diagnostics;
using Microsoft.Extensions.Options;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Assistant Turn Runtime
/// </summary>
/// <param name="promptAssembler">The prompt assembler</param>
/// <param name="providers">The providers</param>
/// <param name="toolCallScheduler">The tool call scheduler</param>
/// <param name="loopDetectionService">The loop detection service</param>
/// <param name="tokenLimitService">The token limit service</param>
/// <param name="providerConfigurationResolver">The provider configuration resolver</param>
/// <param name="options">The options</param>
public sealed class AssistantTurnRuntime(
    IAssistantPromptAssembler promptAssembler,
    IEnumerable<IAssistantResponseProvider> providers,
    IToolCallScheduler toolCallScheduler,
    ILoopDetectionService loopDetectionService,
    ITokenLimitService tokenLimitService,
    ProviderConfigurationResolver providerConfigurationResolver,
    IOptions<NativeAssistantRuntimeOptions> options) : IAssistantTurnRuntime
{
    private readonly IReadOnlyList<IAssistantResponseProvider> _providers = providers.ToArray();
    private readonly NativeAssistantRuntimeOptions _options = options.Value;

    /// <summary>
    /// Generates async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to assistant turn response</returns>
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

        var resolvedConfiguration = providerConfigurationResolver.Resolve(request, _options);
        var tokenLimits = tokenLimitService.Resolve(resolvedConfiguration.Model, _options);
        var promptContext = await promptAssembler.AssembleAsync(request, tokenLimits, cancellationToken);
        if (promptContext.WasBudgetTrimmed)
        {
            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "input-budget-trimmed",
                Message = $"Prompt context was trimmed to fit the input budget for model '{tokenLimits.NormalizedModel}'.",
                ProviderName = _options.Provider,
                Status = "trimmed"
            });
        }
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

            var scheduleResult = await toolCallScheduler.ScheduleAsync(
                request,
                response.ProviderName,
                response.Model,
                response.ToolCalls,
                toolHistory,
                eventSink,
                cancellationToken);

            if (!scheduleResult.ContinueTurnLoop)
            {
                var terminalStats = string.Equals(scheduleResult.TerminalStopReason, "tool-loop-detected", StringComparison.OrdinalIgnoreCase)
                    ? new AssistantExecutionStats
                    {
                        RoundCount = roundCount,
                        ToolCallCount = toolHistory.Count,
                        SuccessfulToolCallCount = 0,
                        FailedToolCallCount = toolHistory.Count,
                        DurationMs = 0
                    }
                    : AssistantExecutionDiagnostics.BuildStats(roundCount, toolHistory);

                return new AssistantTurnResponse
                {
                    Summary = scheduleResult.TerminalSummary,
                    ProviderName = response.ProviderName,
                    Model = response.Model,
                    StopReason = scheduleResult.TerminalStopReason,
                    ToolCalls = response.ToolCalls,
                    ToolExecutions = toolHistory.ToArray(),
                    Stats = terminalStats
                };
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
