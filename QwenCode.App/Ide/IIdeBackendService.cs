using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public interface IIdeBackendService
{
    IdeConnectionSnapshot Inspect(string workspaceRoot, string processCommand = "");

    IdeTransportConnectionInfo? ResolveTransportConnection(string workspaceRoot, string processCommand = "");

    IdeContextSnapshot UpdateContext(IdeContextSnapshot snapshot);

    Task<IdeInstallResult> InstallCompanionAsync(IdeInfo ide, CancellationToken cancellationToken = default);
}
