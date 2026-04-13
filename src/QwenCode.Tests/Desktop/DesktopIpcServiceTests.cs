using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QwenCode.App.AppHost;
using QwenCode.App.Desktop;
using QwenCode.App.Desktop.DirectConnect;
using QwenCode.App.Ipc;
using QwenCode.Core.Models;

namespace QwenCode.Tests.Desktop;

public sealed class DesktopIpcServiceTests
{
    [Fact]
    public async Task StartDirectConnectSessionTurn_ForwardsWrappedTurnToDirectConnectService()
    {
        var directConnect = new RecordingDirectConnectSessionService();
        var service = CreateService(directConnect);

        await service.StartDirectConnectSessionTurn(new StartDirectConnectSessionTurnRequest
        {
            DirectConnectSessionId = "dc-1",
            Turn = new StartDesktopSessionTurnRequest
            {
                Prompt = "Inspect the workspace",
                WorkingDirectory = "D:\\Projects\\workspace"
            }
        });

        Assert.Equal("dc-1", directConnect.LastDirectConnectSessionId);
        Assert.Equal("Inspect the workspace", directConnect.LastStartRequest?.Prompt);
        Assert.Equal("D:\\Projects\\workspace", directConnect.LastStartRequest?.WorkingDirectory);
    }

    [Fact]
    public async Task ReadDirectConnectSessionEvents_ForwardsCursorArguments()
    {
        var directConnect = new RecordingDirectConnectSessionService();
        var service = CreateService(directConnect);

        var batch = await service.ReadDirectConnectSessionEvents(new ReadDirectConnectSessionEventsRequest
        {
            DirectConnectSessionId = "dc-2",
            AfterSequence = 42,
            MaxCount = 25
        });

        Assert.Equal("dc-2", directConnect.LastDirectConnectSessionId);
        Assert.Equal(42, directConnect.LastAfterSequence);
        Assert.Equal(25, directConnect.LastMaxCount);
        Assert.Equal("dc-2", batch.DirectConnectSessionId);
    }

    private static DesktopIpcService CreateService(RecordingDirectConnectSessionService directConnect)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        return new DesktopIpcService(
            services,
            new StubDesktopProjectionService(),
            directConnect,
            new StubDirectConnectServerHost(),
            new DesktopWindowBridge(NullLogger<DesktopWindowBridge>.Instance));
    }

    private sealed class RecordingDirectConnectSessionService : IDirectConnectSessionService
    {
        public string LastDirectConnectSessionId { get; private set; } = string.Empty;
        public StartDesktopSessionTurnRequest? LastStartRequest { get; private set; }
        public long LastAfterSequence { get; private set; }
        public int LastMaxCount { get; private set; }

        public Task<DirectConnectSessionState> CreateSessionAsync(
            CreateDirectConnectSessionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DirectConnectSessionState
            {
                DirectConnectSessionId = "dc-created",
                Status = "active",
                CreatedAtUtc = DateTime.UtcNow,
                LastActivityAtUtc = DateTime.UtcNow
            });

        public Task<IReadOnlyList<DirectConnectSessionState>> ListSessionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DirectConnectSessionState>>([]);

        public Task<DirectConnectSessionState?> GetSessionAsync(
            string directConnectSessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DirectConnectSessionState?>(null);

        public Task<DirectConnectSessionEventBatch> ReadEventsAsync(
            string directConnectSessionId,
            long afterSequence = 0,
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            LastDirectConnectSessionId = directConnectSessionId;
            LastAfterSequence = afterSequence;
            LastMaxCount = maxCount;

            return Task.FromResult(new DirectConnectSessionEventBatch
            {
                DirectConnectSessionId = directConnectSessionId,
                LatestSequence = afterSequence,
                Events = []
            });
        }

        public async IAsyncEnumerable<DirectConnectSessionEventRecord> StreamEventsAsync(
            string directConnectSessionId,
            long afterSequence = 0,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<DesktopSessionTurnResult> StartTurnAsync(
            string directConnectSessionId,
            StartDesktopSessionTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            LastDirectConnectSessionId = directConnectSessionId;
            LastStartRequest = request;

            return Task.FromResult(new DesktopSessionTurnResult
            {
                CreatedNewSession = true,
                AssistantSummary = string.Empty,
                Session = new SessionPreview
                {
                    SessionId = request.SessionId,
                    Title = null,
                    LastActivity = DateTime.UtcNow.ToString("O"),
                    StartedAt = DateTime.UtcNow.ToString("O"),
                    LastUpdatedAt = DateTime.UtcNow.ToString("O"),
                    Category = "session",
                    Mode = DesktopMode.Code,
                    Status = "active",
                    WorkingDirectory = request.WorkingDirectory,
                    GitBranch = "main",
                    MessageCount = 1,
                    TranscriptPath = "chat.jsonl",
                    MetadataPath = "chat.meta.json"
                },
                ToolExecution = new NativeToolExecutionResult
                {
                    ToolName = "chat",
                    Status = "completed",
                    ApprovalState = "approved",
                    WorkingDirectory = request.WorkingDirectory,
                    ChangedFiles = []
                }
            });
        }

        public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
            string directConnectSessionId,
            ApproveDesktopSessionToolRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
            string directConnectSessionId,
            AnswerDesktopSessionQuestionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
            string directConnectSessionId,
            CancelDesktopSessionTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
            string directConnectSessionId,
            ResumeInterruptedTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
            string directConnectSessionId,
            DismissInterruptedTurnRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DirectConnectSessionState> CloseSessionAsync(
            string directConnectSessionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubDesktopProjectionService : IDesktopProjectionService
    {
        public event EventHandler<DesktopStateChangedEvent>? StateChanged;
        public event EventHandler<DesktopSessionEvent>? SessionEvent;
        public event EventHandler<AuthStatusSnapshot>? AuthChanged;
        public event EventHandler<ArenaSessionEvent>? ArenaEvent;

        public Task<AppBootstrapPayload> GetBootstrapAsync() => throw new NotSupportedException();
        public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() => throw new NotSupportedException();
        public Task<IReadOnlyList<ActiveArenaSessionState>> GetActiveArenaSessionsAsync() => throw new NotSupportedException();
        public Task<CancelArenaSessionResult> CancelArenaSessionAsync(CancelArenaSessionRequest request) => throw new NotSupportedException();
        public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) => throw new NotSupportedException();
        public Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request) => throw new NotSupportedException();
        public Task<RenameDesktopSessionResult> RenameSessionAsync(RenameDesktopSessionRequest request) => throw new NotSupportedException();
        public Task<DesktopStateChangedEvent> SetLocaleAsync(string locale) => throw new NotSupportedException();
        public Task<AuthStatusSnapshot> GetAuthStatusAsync() => throw new NotSupportedException();
        public Task<AuthStatusSnapshot> ConfigureOpenAiCompatibleAuthAsync(ConfigureOpenAiCompatibleAuthRequest request) => throw new NotSupportedException();
        public Task<AuthStatusSnapshot> ConfigureCodingPlanAuthAsync(ConfigureCodingPlanAuthRequest request) => throw new NotSupportedException();
        public Task<AuthStatusSnapshot> ConfigureQwenOAuthAsync(ConfigureQwenOAuthRequest request) => throw new NotSupportedException();
        public Task<AuthStatusSnapshot> StartQwenOAuthDeviceFlowAsync(StartQwenOAuthDeviceFlowRequest request) => throw new NotSupportedException();
        public Task<AuthStatusSnapshot> CancelQwenOAuthDeviceFlowAsync(CancelQwenOAuthDeviceFlowRequest request) => throw new NotSupportedException();
        public Task<AuthStatusSnapshot> DisconnectAuthAsync(DisconnectAuthRequest request) => throw new NotSupportedException();
        public Task<ChannelPairingSnapshot> GetChannelPairingsAsync(GetChannelPairingRequest request) => throw new NotSupportedException();
        public Task<ChannelPairingSnapshot> ApproveChannelPairingAsync(ApproveChannelPairingRequest request) => throw new NotSupportedException();
        public Task<WorkspaceSnapshot> GetWorkspaceSnapshotAsync() => throw new NotSupportedException();
        public Task<WorkspaceSnapshot> CreateGitCheckpointAsync(CreateGitCheckpointRequest request) => throw new NotSupportedException();
        public Task<WorkspaceSnapshot> RestoreGitCheckpointAsync(RestoreGitCheckpointRequest request) => throw new NotSupportedException();
        public Task<WorkspaceSnapshot> CreateManagedWorktreeAsync(CreateManagedWorktreeRequest request) => throw new NotSupportedException();
        public Task<WorkspaceSnapshot> CleanupManagedSessionAsync(CleanupManagedWorktreeSessionRequest request) => throw new NotSupportedException();
        public Task<McpSnapshot> AddMcpServerAsync(McpServerRegistrationRequest request) => throw new NotSupportedException();
        public Task<McpSnapshot> RemoveMcpServerAsync(RemoveMcpServerRequest request) => throw new NotSupportedException();
        public Task<McpSnapshot> ReconnectMcpServerAsync(ReconnectMcpServerRequest request) => throw new NotSupportedException();
        public Task<PromptRegistrySnapshot> GetPromptRegistryAsync(GetPromptRegistryRequest request) => throw new NotSupportedException();
        public Task<McpPromptInvocationResult> InvokeRegisteredPromptAsync(InvokePromptRegistryEntryRequest request) => throw new NotSupportedException();
        public Task<McpResourceRegistrySnapshot> GetMcpResourceRegistryAsync(GetMcpResourceRegistryRequest request) => throw new NotSupportedException();
        public Task<McpResourceReadResult> ReadRegisteredMcpResourceAsync(ReadMcpResourceRegistryEntryRequest request) => throw new NotSupportedException();
        public Task<ExtensionSettingsSnapshot> GetExtensionSettingsAsync(GetExtensionSettingsRequest request) => throw new NotSupportedException();
        public Task<ExtensionSnapshot> InstallExtensionAsync(InstallExtensionRequest request) => throw new NotSupportedException();
        public Task<ExtensionConsentSnapshot> PreviewExtensionConsentAsync(InstallExtensionRequest request) => throw new NotSupportedException();
        public Task<ExtensionScaffoldSnapshot> CreateExtensionScaffoldAsync(CreateExtensionScaffoldRequest request) => throw new NotSupportedException();
        public Task<ExtensionSnapshot> UpdateExtensionAsync(UpdateExtensionRequest request) => throw new NotSupportedException();
        public Task<ExtensionSettingsSnapshot> SetExtensionSettingAsync(SetExtensionSettingValueRequest request) => throw new NotSupportedException();
        public Task<ExtensionSnapshot> SetExtensionEnabledAsync(SetExtensionEnabledRequest request) => throw new NotSupportedException();
        public Task<ExtensionSnapshot> RemoveExtensionAsync(RemoveExtensionRequest request) => throw new NotSupportedException();
        public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) => throw new NotSupportedException();
        public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request) => throw new NotSupportedException();
        public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) => throw new NotSupportedException();
        public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) => throw new NotSupportedException();
        public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) => throw new NotSupportedException();
        public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) => throw new NotSupportedException();
        public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) => throw new NotSupportedException();
        public Task<FollowupSuggestionSnapshot> GetFollowupSuggestionsAsync(GetFollowupSuggestionsRequest request) => throw new NotSupportedException();
    }

    private sealed class StubDirectConnectServerHost : IDirectConnectServerHost
    {
        public DirectConnectServerState State { get; } = new()
        {
            Enabled = true,
            Listening = true,
            BaseUrl = "http://127.0.0.1:12345",
            AccessToken = "test-token"
        };

        public Task<DirectConnectServerState> StartAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task StopAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
