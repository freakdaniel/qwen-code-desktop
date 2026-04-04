using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public interface IIdeCompanionTransport
{
    Task<bool> ConnectAsync(IdeTransportConnectionInfo connection, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken = default);

    Task<IdeToolCallResult> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default);
}
