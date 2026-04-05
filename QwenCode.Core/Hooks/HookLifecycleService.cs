using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

public sealed class HookLifecycleService(
    HookRegistryService registryService,
    HookCommandRunner hookCommandRunner,
    HookOutputAggregator aggregator) : IHookLifecycleService
{
    public async Task<HookLifecycleResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        HookInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = registryService.BuildPlan(runtimeProfile, request);
        if (!plan.Enabled || plan.Hooks.Count == 0)
        {
            return new HookLifecycleResult();
        }

        var executions = plan.Sequential
            ? await ExecuteSequentialAsync(plan.Hooks, request, cancellationToken)
            : await ExecuteParallelAsync(plan.Hooks, request, cancellationToken);
        var aggregate = aggregator.Aggregate(executions);
        var blocked = string.Equals(aggregate.Decision, "block", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(aggregate.Decision, "deny", StringComparison.OrdinalIgnoreCase) ||
                      aggregate.Continue == false;

        return new HookLifecycleResult
        {
            AggregateOutput = aggregate,
            IsBlocked = blocked,
            BlockReason = blocked
                ? (string.IsNullOrWhiteSpace(aggregate.Reason)
                    ? $"{request.EventName} was blocked by a configured hook."
                    : aggregate.Reason)
                : string.Empty,
            Executions = executions
        };
    }

    private async Task<IReadOnlyList<HookExecutionResult>> ExecuteParallelAsync(
        IReadOnlyList<CommandHookConfiguration> hooks,
        HookInvocationRequest request,
        CancellationToken cancellationToken)
    {
        var tasks = hooks.Select(hook => hookCommandRunner.ExecuteAsync(hook, request, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private async Task<IReadOnlyList<HookExecutionResult>> ExecuteSequentialAsync(
        IReadOnlyList<CommandHookConfiguration> hooks,
        HookInvocationRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<HookExecutionResult>(hooks.Count);
        var currentRequest = request;

        foreach (var hook in hooks)
        {
            var execution = await hookCommandRunner.ExecuteAsync(hook, currentRequest, cancellationToken);
            results.Add(execution);

            var output = execution.Output;
            if (output is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(output.ModifiedPrompt))
            {
                currentRequest = new HookInvocationRequest
                {
                    EventName = currentRequest.EventName,
                    SessionId = currentRequest.SessionId,
                    WorkingDirectory = currentRequest.WorkingDirectory,
                    TranscriptPath = currentRequest.TranscriptPath,
                    Prompt = output.ModifiedPrompt,
                    ToolName = currentRequest.ToolName,
                    ToolStatus = currentRequest.ToolStatus,
                    ApprovalState = currentRequest.ApprovalState,
                    ToolArgumentsJson = currentRequest.ToolArgumentsJson,
                    ToolOutput = currentRequest.ToolOutput,
                    ToolErrorMessage = currentRequest.ToolErrorMessage,
                    AgentName = currentRequest.AgentName,
                    Reason = currentRequest.Reason,
                    Metadata = (JsonObject)currentRequest.Metadata.DeepClone()
                };
            }
        }

        return results;
    }
}
