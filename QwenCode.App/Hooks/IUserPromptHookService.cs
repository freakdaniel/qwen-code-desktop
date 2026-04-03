using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

public interface IUserPromptHookService
{
    Task<UserPromptHookResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        UserPromptHookRequest request,
        CancellationToken cancellationToken = default);
}
