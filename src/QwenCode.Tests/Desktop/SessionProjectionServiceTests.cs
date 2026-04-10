using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Desktop;
using QwenCode.App.Desktop.Projection;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;
using QwenCode.App.Options;
using QwenCode.Core.Sessions;
using QwenCode.Tests.Shared.Fakes;

namespace QwenCode.Tests.Desktop;

public sealed class SessionProjectionServiceTests
{
    [Fact]
    public async Task StartSessionTurnAsync_NewSession_EnqueuesTitleGeneration()
    {
        var root = Path.Combine(Path.GetTempPath(), $"qwen-session-projection-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var options = Options.Create(new DesktopShellOptions
            {
                DefaultLocale = "ru",
                Workspace = new WorkspacePaths
                {
                    WorkspaceRoot = workspaceRoot
                }
            });
            var environmentPaths = new FakeDesktopEnvironmentPaths(root, root, workspaceRoot, workspaceRoot);
            var workspacePathResolver = new WorkspacePathResolver(environmentPaths);
            var runtimeProfileService = new QwenRuntimeProfileService(environmentPaths);
            var transcriptStore = new DesktopSessionCatalogService(runtimeProfileService, new ChatRecordingService());
            var runtimeProfile = runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = workspaceRoot });
            Directory.CreateDirectory(runtimeProfile.ChatsDirectory);

            var result = new DesktopSessionTurnResult
            {
                CreatedNewSession = true,
                AssistantSummary = string.Empty,
                Session = new SessionPreview
                {
                    SessionId = "session-1",
                    Title = null,
                    LastActivity = DateTime.UtcNow.ToString("O"),
                    StartedAt = DateTime.UtcNow.ToString("O"),
                    LastUpdatedAt = DateTime.UtcNow.ToString("O"),
                    Category = "session",
                    Mode = DesktopMode.Code,
                    Status = "active",
                    WorkingDirectory = workspaceRoot,
                    GitBranch = "main",
                    MessageCount = 1,
                    TranscriptPath = Path.Combine(runtimeProfile.ChatsDirectory, "session-1.jsonl"),
                    MetadataPath = Path.Combine(runtimeProfile.ChatsDirectory, "session-1.meta.json")
                },
                ToolExecution = new NativeToolExecutionResult
                {
                    ToolName = "chat",
                    Status = "completed",
                    ApprovalState = "approved",
                    WorkingDirectory = workspaceRoot,
                    ChangedFiles = []
                }
            };
            var sessionHost = new RecordingSessionHost(result);
            var titleGenerationService = new RecordingSessionTitleGenerationService();

            var service = new SessionProjectionService(
                options,
                workspacePathResolver,
                transcriptStore,
                (ISessionService)transcriptStore,
                new FakeToolExecutor(),
                sessionHost,
                new ActiveTurnRegistry(new InterruptedTurnStore()),
                runtimeProfileService,
                new ServiceCollection()
                    .AddSingleton<ISessionTitleGenerationService>(titleGenerationService)
                    .BuildServiceProvider(),
                new LocaleStateService(options));

            await service.StartSessionTurnAsync(new StartDesktopSessionTurnRequest
            {
                SessionId = "session-1",
                Prompt = "Compare Avalonia and WPF architecture",
                WorkingDirectory = workspaceRoot
            });

            var call = Assert.Single(titleGenerationService.Calls);
            Assert.Equal("session-1", call.SessionId);
            Assert.Equal("Compare Avalonia and WPF architecture", call.FirstMessageText);
            Assert.Equal(result.Session.TranscriptPath, call.TranscriptPath);
            Assert.Equal(workspaceRoot, call.WorkingDirectory);
            Assert.Equal("ru", call.Locale);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class RecordingSessionHost(DesktopSessionTurnResult result) : ISessionHost
    {
        public event EventHandler<DesktopSessionEvent>? SessionEvent;

        public Task<DesktopSessionTurnResult> StartTurnAsync(
            WorkspacePaths paths,
            StartDesktopSessionTurnRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(result);

        public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(
            WorkspacePaths paths,
            ApproveDesktopSessionToolRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(
            WorkspacePaths paths,
            AnswerDesktopSessionQuestionRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CancelDesktopSessionTurnResult> CancelTurnAsync(
            WorkspacePaths paths,
            CancelDesktopSessionTurnRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(
            WorkspacePaths paths,
            ResumeInterruptedTurnRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(
            WorkspacePaths paths,
            DismissInterruptedTurnRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingSessionTitleGenerationService : ISessionTitleGenerationService
    {
        public List<TitleGenerationCall> Calls { get; } = [];

        public void EnqueueTitleGeneration(
            string sessionId,
            string firstMessageText,
            string transcriptPath,
            string workingDirectory,
            string locale) =>
            Calls.Add(new TitleGenerationCall(sessionId, firstMessageText, transcriptPath, workingDirectory, locale));
    }

    private sealed record TitleGenerationCall(
        string SessionId,
        string FirstMessageText,
        string TranscriptPath,
        string WorkingDirectory,
        string Locale);
}
