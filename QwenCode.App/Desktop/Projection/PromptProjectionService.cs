using Microsoft.Extensions.Options;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Prompts;

namespace QwenCode.App.Desktop;

public sealed class PromptProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IPromptRegistryService promptRegistryService) : IDesktopPromptProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    public Task<PromptRegistrySnapshot> GetPromptRegistryAsync(
        GetPromptRegistryRequest request,
        CancellationToken cancellationToken = default) =>
        promptRegistryService.GetSnapshotAsync(ResolveWorkspace(), request, cancellationToken);

    public Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(
        InvokePromptRegistryEntryRequest request,
        CancellationToken cancellationToken = default) =>
        promptRegistryService.InvokeAsync(ResolveWorkspace(), request, cancellationToken);

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
