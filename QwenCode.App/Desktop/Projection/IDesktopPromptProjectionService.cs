using QwenCode.App.Models;

namespace QwenCode.App.Desktop;

public interface IDesktopPromptProjectionService
{
    Task<PromptRegistrySnapshot> GetPromptRegistryAsync(
        GetPromptRegistryRequest request,
        CancellationToken cancellationToken = default);

    Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(
        InvokePromptRegistryEntryRequest request,
        CancellationToken cancellationToken = default);
}
