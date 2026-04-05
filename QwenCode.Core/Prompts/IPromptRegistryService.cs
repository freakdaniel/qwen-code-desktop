using QwenCode.App.Models;

namespace QwenCode.App.Prompts;

public interface IPromptRegistryService
{
    Task<PromptRegistrySnapshot> GetSnapshotAsync(
        WorkspacePaths paths,
        GetPromptRegistryRequest request,
        CancellationToken cancellationToken = default);

    Task<McpPromptInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        InvokePromptRegistryEntryRequest request,
        CancellationToken cancellationToken = default);
}
