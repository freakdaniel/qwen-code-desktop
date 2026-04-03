using Microsoft.Extensions.Options;
using QwenCode.App.Channels;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Options;

namespace QwenCode.App.Desktop;

public sealed class ChannelProjectionService(
    IOptions<DesktopShellOptions> options,
    IWorkspacePathResolver workspacePathResolver,
    IChannelRegistryService channelRegistryService) : IDesktopChannelProjectionService
{
    private readonly DesktopShellOptions shellOptions = options.Value;

    public ChannelSnapshot CreateSnapshot() =>
        channelRegistryService.Inspect(ResolveWorkspace());

    public Task<ChannelPairingSnapshot> GetPairingsAsync(GetChannelPairingRequest request) =>
        Task.FromResult(channelRegistryService.GetPairings(ResolveWorkspace(), request));

    public Task<ChannelPairingSnapshot> ApprovePairingAsync(ApproveChannelPairingRequest request) =>
        Task.FromResult(channelRegistryService.ApprovePairing(ResolveWorkspace(), request));

    private WorkspacePaths ResolveWorkspace() => workspacePathResolver.Resolve(shellOptions.Workspace);
}
