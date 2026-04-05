using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

/// <summary>
/// Represents the User Prompt Hook Service
/// </summary>
/// <param name="hookLifecycleService">The hook lifecycle service</param>
public sealed class UserPromptHookService(
    IHookLifecycleService hookLifecycleService) : IUserPromptHookService
{
    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to user prompt hook result</returns>
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
