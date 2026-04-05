using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

public sealed class UserPromptHookService(
    IHookLifecycleService hookLifecycleService) : IUserPromptHookService
{
    public async Task<UserPromptHookResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        UserPromptHookRequest request,
        CancellationToken cancellationToken = default)
    {
        var lifecycleResult = await hookLifecycleService.ExecuteAsync(
            runtimeProfile,
            new HookInvocationRequest
            {
                EventName = HookEventName.UserPromptSubmit,
                SessionId = request.SessionId,
                Prompt = request.Prompt,
                WorkingDirectory = request.WorkingDirectory,
                TranscriptPath = request.TranscriptPath
            },
            cancellationToken);
        var aggregate = lifecycleResult.AggregateOutput;
        var effectivePrompt = string.IsNullOrWhiteSpace(aggregate.ModifiedPrompt)
            ? request.Prompt
            : aggregate.ModifiedPrompt;

        return new UserPromptHookResult
        {
            EffectivePrompt = effectivePrompt,
            AdditionalContext = aggregate.AdditionalContext,
            SystemMessage = aggregate.SystemMessage,
            IsBlocked = lifecycleResult.IsBlocked,
            BlockReason = lifecycleResult.BlockReason,
            Executions = lifecycleResult.Executions
        };
    }
}
