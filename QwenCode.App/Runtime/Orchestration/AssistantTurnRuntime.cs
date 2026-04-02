using Microsoft.Extensions.Options;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Tools;

namespace QwenCode.App.Runtime;

public sealed class AssistantTurnRuntime(
    IAssistantPromptAssembler promptAssembler,
    IEnumerable<IAssistantResponseProvider> providers,
    IToolExecutor toolExecutor,
    IOptions<NativeAssistantRuntimeOptions> options) : IAssistantTurnRuntime
{
    private readonly IReadOnlyList<IAssistantResponseProvider> _providers = providers.ToArray();
    private readonly NativeAssistantRuntimeOptions _options = options.Value;

    public async Task<AssistantTurnResponse> GenerateAsync(
        AssistantTurnRequest request,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
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
        var maxIterations = Math.Max(1, _options.MaxToolIterations);
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var response = await provider.TryGenerateAsync(
                request,
                promptContext,
                toolHistory,
                _options,
                eventSink,
                cancellationToken);
            if (response is null)
            {
                return null;
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

            foreach (var toolCall in response.ToolCalls)
            {
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
            Model = _options.Model,
            ToolExecutions = toolHistory.ToArray()
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
}
