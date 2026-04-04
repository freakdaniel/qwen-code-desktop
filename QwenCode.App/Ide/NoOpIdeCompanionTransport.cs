using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Ide;

public sealed class NoOpIdeCompanionTransport : IIdeCompanionTransport
{
    public Task<bool> ConnectAsync(IdeTransportConnectionInfo connection, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IdeToolCallResult> CallToolAsync(
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new IdeToolCallResult
        {
            IsError = true,
            Text = "IDE companion transport is not connected."
        });
}
