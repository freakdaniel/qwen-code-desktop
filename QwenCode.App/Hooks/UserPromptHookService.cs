using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

public sealed class UserPromptHookService(
    HookRegistryService registryService,
    HookCommandRunner hookCommandRunner,
    HookOutputAggregator aggregator) : IUserPromptHookService
{
    public async Task<UserPromptHookResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        UserPromptHookRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = registryService.BuildUserPromptSubmitPlan(runtimeProfile);
        if (!plan.Enabled || plan.Hooks.Count == 0)
        {
            return new UserPromptHookResult
            {
                EffectivePrompt = request.Prompt
            };
        }

        var executions = plan.Sequential
            ? await ExecuteSequentialAsync(plan.Hooks, request, cancellationToken)
            : await ExecuteParallelAsync(plan.Hooks, request, cancellationToken);

        var aggregate = aggregator.Aggregate(executions);
        var effectivePrompt = ResolveEffectivePrompt(request.Prompt, executions, aggregate, plan.Sequential);
        var blocked = string.Equals(aggregate.Decision, "block", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(aggregate.Decision, "deny", StringComparison.OrdinalIgnoreCase);

        return new UserPromptHookResult
        {
            EffectivePrompt = effectivePrompt,
            AdditionalContext = aggregate.AdditionalContext,
            SystemMessage = aggregate.SystemMessage,
            IsBlocked = blocked,
            BlockReason = blocked
                ? (string.IsNullOrWhiteSpace(aggregate.Reason) ? "Prompt was blocked by a configured hook." : aggregate.Reason)
                : string.Empty,
            Executions = executions
        };
    }

    private async Task<IReadOnlyList<HookExecutionResult>> ExecuteParallelAsync(
        IReadOnlyList<CommandHookConfiguration> hooks,
        UserPromptHookRequest request,
        CancellationToken cancellationToken)
    {
        var tasks = hooks.Select(hook => hookCommandRunner.ExecuteAsync(hook, request, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private async Task<IReadOnlyList<HookExecutionResult>> ExecuteSequentialAsync(
        IReadOnlyList<CommandHookConfiguration> hooks,
        UserPromptHookRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<HookExecutionResult>(hooks.Count);
        var currentPrompt = request.Prompt;

        foreach (var hook in hooks)
        {
            var execution = await hookCommandRunner.ExecuteAsync(
                hook,
                new UserPromptHookRequest
                {
                    SessionId = request.SessionId,
                    Prompt = currentPrompt,
                    WorkingDirectory = request.WorkingDirectory,
                    TranscriptPath = request.TranscriptPath
                },
                cancellationToken);
            results.Add(execution);

            var output = execution.Output;
            if (output is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(output.ModifiedPrompt))
            {
                currentPrompt = output.ModifiedPrompt;
            }

            if (!string.IsNullOrWhiteSpace(output.AdditionalContext))
            {
                currentPrompt = $"{currentPrompt}{Environment.NewLine}{Environment.NewLine}{output.AdditionalContext}";
            }
        }

        return results;
    }

    private static string ResolveEffectivePrompt(
        string originalPrompt,
        IReadOnlyList<HookExecutionResult> executions,
        HookOutput aggregate,
        bool sequential)
    {
        if (sequential && executions.Count > 0)
        {
            for (var index = executions.Count - 1; index >= 0; index--)
            {
                var output = executions[index].Output;
                if (!string.IsNullOrWhiteSpace(output?.ModifiedPrompt))
                {
                    return output.ModifiedPrompt;
                }
            }
        }

        return string.IsNullOrWhiteSpace(aggregate.ModifiedPrompt)
            ? originalPrompt
            : aggregate.ModifiedPrompt;
    }
}
