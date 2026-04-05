using System.Text.Json.Nodes;
using QwenCode.App.Ide;

namespace QwenCode.Tests.Ide;

public sealed class IdeClientServiceTests
{
    [Fact]
    public async Task IdeClientService_ConnectAsync_LoadsToolsAndEnablesDiffing()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "qwen-ide-client-workspace");
        var backend = new FakeIdeBackendService(new IdeConnectionSnapshot
        {
            Status = "connected",
            WorkspacePath = workspacePath,
            Port = "4111"
        });
        var transport = new FakeIdeCompanionTransport
        {
            ConnectResult = true,
            Tools = ["openDiff", "closeDiff", "getDiagnostics"]
        };
        var service = new IdeClientService(backend, transport);

        var snapshot = await service.ConnectAsync(workspacePath);

        Assert.Equal("connected", snapshot.Status);
        Assert.True(snapshot.SupportsDiff);
        Assert.Contains("openDiff", snapshot.AvailableTools);
    }

    [Fact]
    public async Task IdeClientService_OpenDiffAsync_ResolvesThroughCliDecision()
    {
        var workspacePath = Path.Combine(Path.GetTempPath(), "qwen-ide-client-workspace");
        var filePath = Path.Combine(workspacePath, "a.cs");
        var backend = new FakeIdeBackendService(new IdeConnectionSnapshot
        {
            Status = "connected",
            WorkspacePath = workspacePath,
            Port = "4111"
        });
        var transport = new FakeIdeCompanionTransport
        {
            ConnectResult = true,
            Tools = ["openDiff", "closeDiff"],
            CloseDiffContent = """{"content":"patched"}"""
        };
        var service = new IdeClientService(backend, transport);
        _ = await service.ConnectAsync(workspacePath);

        var pending = service.OpenDiffAsync(filePath, "new-content");
        await Task.Delay(20);
        await service.ResolveDiffFromCliAsync(filePath, "accepted");
        var result = await pending;

        Assert.Equal("accepted", result.Status);
        Assert.Equal("patched", result.Content);
    }

    private sealed class FakeIdeBackendService(IdeConnectionSnapshot snapshot) : IIdeBackendService
    {
        public IdeConnectionSnapshot Inspect(string workspaceRoot, string processCommand = "") => snapshot;

        public IdeTransportConnectionInfo? ResolveTransportConnection(string workspaceRoot, string processCommand = "") =>
            new()
            {
                WorkspacePath = snapshot.WorkspacePath,
                Port = snapshot.Port
            };

        public IdeContextSnapshot UpdateContext(IdeContextSnapshot snapshot) => snapshot;

        public Task<IdeInstallResult> InstallCompanionAsync(IdeInfo ide, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IdeInstallResult { Success = true, Message = "ok" });
    }

    private sealed class FakeIdeCompanionTransport : IIdeCompanionTransport
    {
        public bool ConnectResult { get; init; }

        public IReadOnlyList<string> Tools { get; init; } = [];

        public string CloseDiffContent { get; init; } = string.Empty;

        public Task<bool> ConnectAsync(IdeTransportConnectionInfo connection, CancellationToken cancellationToken = default) =>
            Task.FromResult(ConnectResult);

        public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListToolsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Tools);

        public Task<IdeToolCallResult> CallToolAsync(string toolName, JsonObject arguments, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IdeToolCallResult
            {
                IsError = false,
                Text = string.Equals(toolName, "closeDiff", StringComparison.OrdinalIgnoreCase)
                    ? CloseDiffContent
                    : string.Empty
            });
    }
}
