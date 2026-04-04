using Microsoft.Extensions.Options;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

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

    public async Task<AssistantTurnResponse> GenerateAsync(
        AssistantTurnRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
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
                    return response;
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
                    ToolExecutions = toolHistory.ToArray()
                };
            }

            if (response.ToolCalls.Count == 0)
            {
                return new AssistantTurnResponse
                {
                    Summary = response.Summary,
                    ProviderName = response.ProviderName,
                    Model = response.Model,
                    ToolExecutions = toolHistory.ToArray()
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
                return new AssistantTurnResponse
                {
                    Summary = scheduleResult.TerminalSummary,
                    ProviderName = response.ProviderName,
                    Model = response.Model,
                    ToolExecutions = toolHistory.ToArray()
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
            Model = _options.Model,
            ToolExecutions = toolHistory.ToArray()
        };
    }

}
