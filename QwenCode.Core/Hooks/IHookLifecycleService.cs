using QwenCode.App.Models;

namespace QwenCode.App.Hooks;

public interface IHookLifecycleService
{
    Task<HookLifecycleResult> ExecuteAsync(
        QwenRuntimeProfile runtimeProfile,
        HookInvocationRequest request,
        CancellationToken cancellationToken = default);
}
