using Microsoft.Extensions.Options;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;
using QwenCode.App.Options;
using QwenCode.Core.Prompts;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Prompt Projection Service
/// </summary>
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="promptRegistryService">The prompt registry service</param>
public sealed class PromptProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IPromptRegistryService promptRegistryService) : IDesktopPromptProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    /// <summary>
    /// Gets prompt registry async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to prompt registry snapshot</returns>
    public Task<PromptRegistrySnapshot> GetPromptRegistryAsync(
        GetPromptRegistryRequest request,
        CancellationToken cancellationToken = default) =>
        promptRegistryService.GetSnapshotAsync(ResolveWorkspace(), request, cancellationToken);

    /// <summary>
    /// Invokes registered prompt async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    public Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(
        InvokePromptRegistryEntryRequest request,
        CancellationToken cancellationToken = default) =>
        promptRegistryService.InvokeAsync(ResolveWorkspace(), request, cancellationToken);

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
