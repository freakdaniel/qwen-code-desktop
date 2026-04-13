using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using QwenCode.App.Desktop.Projection;
using QwenCode.Core.Compatibility;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;
using QwenCode.App.Options;
using QwenCode.Core.Sessions;
using QwenCode.Core.Tools;

namespace QwenCode.App.Desktop;

/// <summary>
/// Represents the Session Projection Service
/// </summary>
public sealed class SessionProjectionService : IDesktopSessionProjectionService, ISessionEventPublisher
{
    private readonly DesktopShellOptions _options;
    private readonly IWorkspacePathResolver _workspacePathResolver;
    private readonly ITranscriptStore _transcriptStore;
    private readonly ISessionService _sessionService;
    private readonly IToolExecutor _toolExecutor;
    private readonly ISessionHost _sessionHost;
    private readonly IActiveTurnRegistry _activeTurnRegistry;
    private readonly QwenRuntimeProfileService _runtimeProfileService;
    private readonly IServiceProvider _services;
    private readonly ILocaleStateService _localeStateService;
    private EventHandler<DesktopSessionEvent>? _sessionEvent;

    /// <summary>
    /// Initializes a new instance of <see cref="SessionProjectionService"/>
    /// </summary>
    /// <param name="options">The options</param>
    /// <param name="workspacePathResolver">The workspace path resolver</param>
    /// <param name="transcriptStore">The transcript store</param>
    /// <param name="sessionService">The session service</param>
    /// <param name="toolExecutor">The tool executor</param>
    /// <param name="sessionHost">The session host</param>
    /// <param name="activeTurnRegistry">The active turn registry</param>
    /// <param name="runtimeProfileService">The runtime profile service</param>
    /// <param name="services">The services</param>
    /// <param name="localeStateService">The locale state service</param>
    public SessionProjectionService(
        IOptions<DesktopShellOptions> options,
        IWorkspacePathResolver workspacePathResolver,
        ITranscriptStore transcriptStore,
        ISessionService sessionService,
        IToolExecutor toolExecutor,
        ISessionHost sessionHost,
        IActiveTurnRegistry activeTurnRegistry,
        QwenRuntimeProfileService runtimeProfileService,
        IServiceProvider services,
        ILocaleStateService localeStateService)
    {
        _options = options.Value;
        _workspacePathResolver = workspacePathResolver;
        _transcriptStore = transcriptStore;
        _sessionService = sessionService;
        _toolExecutor = toolExecutor;
        _sessionHost = sessionHost;
        _activeTurnRegistry = activeTurnRegistry;
        _runtimeProfileService = runtimeProfileService;
        _services = services;
        _localeStateService = localeStateService;

        _sessionHost.SessionEvent += (s, e) => _sessionEvent?.Invoke(s, e);
    }

    /// <summary>
    /// Occurs when Session Event
    /// </summary>
    public event EventHandler<DesktopSessionEvent>? SessionEvent
    {
        add => _sessionEvent += value;
        remove => _sessionEvent -= value;
    }

    /// <inheritdoc/>
    public void Publish(DesktopSessionEvent sessionEvent) =>
        _sessionEvent?.Invoke(this, sessionEvent);

    /// <summary>
    /// Gets active turns async
    /// </summary>
    /// <returns>A task that resolves to i read only list active turn state</returns>
    public Task<IReadOnlyList<ActiveTurnState>> GetActiveTurnsAsync() =>
        Task.FromResult(_activeTurnRegistry.ListActiveTurns());

    /// <summary>
    /// Gets session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session detail?</returns>
    public Task<DesktopSessionDetail?> GetSessionAsync(GetDesktopSessionRequest request) =>
        Task.FromResult(_transcriptStore.GetSession(ResolveWorkspace(), request));

    /// <summary>
    /// Removes session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to remove desktop session result</returns>
    public Task<RemoveDesktopSessionResult> RemoveSessionAsync(RemoveDesktopSessionRequest request)
    {
        var workspace = ResolveWorkspace();
        var removed = _sessionService.RemoveSession(workspace, request.SessionId);
        return Task.FromResult(new RemoveDesktopSessionResult
        {
            Removed = removed,
            SessionId = request.SessionId,
            RecentSessions = _transcriptStore.ListSessions(workspace)
        });
    }

    /// <summary>
    /// Renames session async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to rename desktop session result</returns>
    public Task<RenameDesktopSessionResult> RenameSessionAsync(RenameDesktopSessionRequest request)
    {
        var workspace = ResolveWorkspace();
        var title = request.Title.Trim();
        var renamed = _sessionService.RenameSession(workspace, request.SessionId, title);
        return Task.FromResult(new RenameDesktopSessionResult
        {
            Renamed = renamed,
            SessionId = request.SessionId,
            Title = title,
            RecentSessions = _transcriptStore.ListSessions(workspace)
        });
    }

    /// <summary>
    /// Executes native tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    public Task<NativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
        _toolExecutor.ExecuteAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Starts session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public async Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request)
    {
        var workspace = ResolveWorkspace();
        var runtimeProfile = _runtimeProfileService.Inspect(workspace);
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString() : request.SessionId;
        var transcriptPath = Path.Combine(runtimeProfile.ChatsDirectory, $"{sessionId}.jsonl");
        var workingDirectory = ResolveWorkingDirectory(runtimeProfile, request.WorkingDirectory);
        var createdNewSession = !_sessionService.SessionExists(workspace, sessionId);
        var normalizedRequest = new StartDesktopSessionTurnRequest
        {
            SessionId = sessionId,
            Prompt = request.Prompt,
            WorkingDirectory = request.WorkingDirectory,
            SurfaceContext = request.SurfaceContext,
            ToolName = request.ToolName,
            ToolArgumentsJson = request.ToolArgumentsJson,
            ApproveToolExecution = request.ApproveToolExecution
        };

        if (createdNewSession && !string.IsNullOrWhiteSpace(normalizedRequest.Prompt))
        {
            var sessionTitleGenerationService = _services.GetRequiredService<ISessionTitleGenerationService>();
            sessionTitleGenerationService.EnqueueTitleGeneration(
                sessionId,
                normalizedRequest.Prompt,
                transcriptPath,
                workingDirectory,
                _localeStateService.CurrentLocale);
        }

        return await _sessionHost.StartTurnAsync(workspace, normalizedRequest);
    }

    /// <summary>
    /// Approves pending tool async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) =>
        _sessionHost.ApprovePendingToolAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes answer pending question async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> AnswerPendingQuestionAsync(AnswerDesktopSessionQuestionRequest request) =>
        _sessionHost.AnswerPendingQuestionAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Cancels session turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to cancel desktop session turn result</returns>
    public Task<CancelDesktopSessionTurnResult> CancelSessionTurnAsync(CancelDesktopSessionTurnRequest request) =>
        _sessionHost.CancelTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes resume interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to desktop session turn result</returns>
    public Task<DesktopSessionTurnResult> ResumeInterruptedTurnAsync(ResumeInterruptedTurnRequest request) =>
        _sessionHost.ResumeInterruptedTurnAsync(ResolveWorkspace(), request);

    /// <summary>
    /// Executes dismiss interrupted turn async
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <returns>A task that resolves to dismiss interrupted turn result</returns>
    public Task<DismissInterruptedTurnResult> DismissInterruptedTurnAsync(DismissInterruptedTurnRequest request) =>
        _sessionHost.DismissInterruptedTurnAsync(ResolveWorkspace(), request);

    private WorkspacePaths ResolveWorkspace() => _workspacePathResolver.Resolve(_options.Workspace);

    private static string ResolveWorkingDirectory(QwenRuntimeProfile runtimeProfile, string requestedWorkingDirectory)
    {
        var workspaceRoot = runtimeProfile.ProjectRoot;
        var runtimeTempRoot = Path.Combine(runtimeProfile.RuntimeBaseDirectory, "tmp");
        var resolved = string.IsNullOrWhiteSpace(requestedWorkingDirectory)
            ? workspaceRoot
            : Path.IsPathRooted(requestedWorkingDirectory)
                ? Path.GetFullPath(requestedWorkingDirectory)
                : Path.GetFullPath(Path.Combine(workspaceRoot, requestedWorkingDirectory));

        if (IsPathWithinRoot(resolved, workspaceRoot) || IsPathWithinRoot(resolved, runtimeTempRoot))
        {
            return resolved;
        }

        return Path.GetFullPath(resolved);
    }

    private static bool IsPathWithinRoot(string path, string root)
    {
        static string NormalizeRoot(string value) =>
            Path.GetFullPath(value)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var normalizedPath = NormalizeRoot(path);
        var normalizedRoot = NormalizeRoot(root);
        return normalizedPath.StartsWith(normalizedRoot, comparison);
    }
}
