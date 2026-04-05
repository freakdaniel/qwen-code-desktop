using Microsoft.Extensions.Options;
using QwenCode.App.Channels;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Channel Projection Service
/// </summary>
/// <param name="options">The options</param>
/// <param name="workspacePathResolver">The workspace path resolver</param>
/// <param name="channelRegistryService">The channel registry service</param>
public sealed class ChannelProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IChannelRegistryService channelRegistryService) : IDesktopChannelProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    /// <summary>
    /// Creates snapshot
    /// </summary>
    /// <returns>The resulting channel snapshot</returns>
    public ChannelSnapshot CreateSnapshot() =>
        channelRegistryService.Inspect(ResolveWorkspace());

    /// <summary>
    /// Gets pairings async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    public Task<ChannelPairingSnapshot> GetPairingsAsync(GetChannelPairingRequest request) =>
        Task.FromResult(channelRegistryService.GetPairings(ResolveWorkspace(), request));

    /// <summary>
    /// Approves pairing async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to channel pairing snapshot</returns>
    public Task<ChannelPairingSnapshot> ApprovePairingAsync(ApproveChannelPairingRequest request) =>
        Task.FromResult(channelRegistryService.ApprovePairing(ResolveWorkspace(), request));

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
