using Microsoft.Extensions.Options;
using QwenCode.App.Enums;
using QwenCode.App.Models;
using QwenCode.App.Options;
using QwenCode.App.Compatibility;
using QwenCode.App.Permissions;
using QwenCode.App.Sessions;
using QwenCode.App.Tools;
using QwenCode.App.Desktop.Diagnostics;

namespace QwenCode.App.Desktop;

public sealed class DesktopAppService(
    IOptions<DesktopShellOptions> options,
    SourceMirrorInspectorService sourceMirrorInspector,
    RuntimePortPlannerService runtimePortPlanner,
    ISettingsResolver settingsResolver,
    IToolRegistry qwenToolCatalogService,
    IToolExecutor qwenNativeToolHostService,
    ITranscriptStore desktopSessionCatalogService,
    ISessionHost desktopSessionHostService) : IDesktopProjectionService
{
    private static readonly IReadOnlyList<LocaleOption> SupportedLocales =
    [
        new() { Code = "en", Name = "English", NativeName = "English" },
        new() { Code = "ru", Name = "Russian", NativeName = "\u0420\u0443\u0441\u0441\u043a\u0438\u0439" },
        new() { Code = "zh-CN", Name = "Chinese", NativeName = "\u7b80\u4f53\u4e2d\u6587" },
        new() { Code = "de", Name = "German", NativeName = "Deutsch" },
        new() { Code = "fr", Name = "French", NativeName = "Francais" },
        new() { Code = "es", Name = "Spanish", NativeName = "Espanol" },
        new() { Code = "ja", Name = "Japanese", NativeName = "\u65e5\u672c\u8a9e" },
        new() { Code = "ko", Name = "Korean", NativeName = "\ud55c\uad6d\uc5b4" },
        new() { Code = "pt-BR", Name = "Portuguese (Brazil)", NativeName = "Portugu\u00eas (Brasil)" },
        new() { Code = "tr", Name = "Turkish", NativeName = "T\u00fcrk\u00e7e" },
        new() { Code = "ar", Name = "Arabic", NativeName = "\u0627\u0644\u0639\u0631\u0628\u064a\u0629" }
    ];

    private static readonly IReadOnlyList<ResearchTrack> Tracks =
    [
        new()
        {
            Title = "Lift qwen core behind a native session host",
            Summary = "The desktop backend should own orchestration, but the model loop, tools, history, and policy logic must stay source-compatible with qwen."
        },
        new()
        {
            Title = "Adopt Claude-grade session ergonomics",
            Summary = "Claude's desktop patterns are strongest around workspaces, session lifecycle, approvals, and context visibility."
        },
        new()
        {
            Title = "Keep the Electron bridge narrow and typed",
            Summary = "Typed preload contracts and strongly shaped payloads make the desktop shell maintainable while the runtime grows underneath."
        }
    ];

    private static readonly IReadOnlyList<string> CompatibilityGoals =
    [
        "Do not shell out to qwen CLI for core execution paths.",
        "Keep .qwen-compatible settings, memory, session, and tool semantics.",
        "Treat claude-code as a UX and session-orchestration reference only.",
        "Make desktop-specific concerns explicit: windows, trays, approvals, attachments, and session chrome."
    ];

    private static readonly IReadOnlyList<CapabilityLane> CapabilityLanes =
    [
        new()
        {
            Title = "Qwen runtime lane",
            Summary = "Owns prompt construction, tool registry, session state, settings, and compatibility with qwen workflows.",
            Responsibilities =
            [
                "Model API integration and turn execution",
                "Tool calling, sandbox policy, and approvals",
                "History, memory, slash commands, and settings compatibility"
            ]
        },
        new()
        {
            Title = "Desktop bridge lane",
            Summary = "Owns native session hosting, IPC, attachment plumbing, notifications, and cross-window coordination.",
            Responsibilities =
            [
                "Session bootstrap and reconnect semantics",
                "Typed IPC between .NET host and renderer",
                "Native file pickers, desktop prompts, and platform integrations"
            ]
        },
        new()
        {
            Title = "Claude-inspired renderer lane",
            Summary = "Owns the high-level interaction model: sidebar navigation, code-first desktop surfaces, approvals, and task visibility.",
            Responsibilities =
            [
                "Home, sessions, customize, projects, and artifacts surfaces",
                "Single code-mode composer and conversation chrome",
                "Tool timeline, approval panels, and architecture guidance"
            ]
        }
    ];

    private static readonly IReadOnlyList<AdoptionPattern> AdoptionPatterns =
    [
        new()
        {
            Area = "Execution engine",
            QwenSource = "packages/core should remain the authority for prompt assembly, tool execution, and session semantics.",
            ClaudeReference = "claude-code adds a session bridge instead of shoving desktop behavior into the renderer.",
            DesktopDirection = "Build a native host around qwen core primitives, not a wrapper around qwen CLI stdout.",
            DeliveryState = "Foundation"
        },
        new()
        {
            Area = "Session lifecycle",
            QwenSource = "CLI and history flows already define how turns, resumes, and config layering work.",
            ClaudeReference = "BridgeConfig, SessionHandle, and session status tracking show how desktop workspaces can reconnect and expose live activity.",
            DesktopDirection = "Promote sessions to first-class desktop objects with reconnect, activity, and branch/worktree awareness.",
            DeliveryState = "High priority"
        },
        new()
        {
            Area = "Approvals and tools",
            QwenSource = "Approval modes and sandbox policies already exist in qwen and should be preserved.",
            ClaudeReference = "Claude's UX makes permission requests, task state, and tool activity visible instead of burying them in terminal text.",
            DesktopDirection = "Move approvals into explicit desktop panels while keeping qwen policy logic intact.",
            DeliveryState = "High priority"
        },
        new()
        {
            Area = "Context surfaces",
            QwenSource = "Settings, memory, slash commands, and project context are already well-defined.",
            ClaudeReference = "Customize, connectors, scheduled work, and project surfaces make these capabilities discoverable.",
            DesktopDirection = "Expose qwen capabilities as browseable desktop surfaces rather than hidden CLI-only concepts.",
            DeliveryState = "In design"
        },
        new()
        {
            Area = "IPC discipline",
            QwenSource = "Core/CLI separation means renderer-specific contracts should stay outside the engine.",
            ClaudeReference = "Separate bridge types and APIs keep desktop traffic structured and evolvable.",
            DesktopDirection = "Keep Electron preload thin and strongly typed so backend evolution does not leak into UI code.",
            DeliveryState = "Implemented"
        }
    ];

    private readonly object _syncRoot = new();
    private readonly DesktopShellOptions _options = options.Value;
    private string _currentLocale = NormalizeLocale(options.Value.DefaultLocale);

    public event EventHandler<DesktopStateChangedEvent>? StateChanged;

    public Task<AppBootstrapPayload> GetBootstrapAsync()
    {
        lock (_syncRoot)
        {
            var qwenRuntime = settingsResolver.InspectRuntimeProfile(_options.Sources);

            return Task.FromResult(new AppBootstrapPayload
            {
                ProductName = _options.ProductName,
                CurrentMode = DesktopMode.Code,
                CurrentLocale = _currentLocale,
                Locales = SupportedLocales,
                Sources = _options.Sources,
                Tracks = Tracks,
                CompatibilityGoals = CompatibilityGoals,
                CapabilityLanes = CapabilityLanes,
                AdoptionPatterns = AdoptionPatterns,
                RecentSessions = desktopSessionCatalogService.ListSessions(_options.Sources),
                SourceStatuses = sourceMirrorInspector.Inspect(_options.Sources),
                RuntimePortPlan = runtimePortPlanner.BuildPlan(_options.Sources),
                QwenCompatibility = settingsResolver.InspectCompatibility(_options.Sources),
                QwenRuntime = qwenRuntime,
                QwenTools = qwenToolCatalogService.Inspect(_options.Sources),
                QwenNativeHost = qwenNativeToolHostService.Inspect(_options.Sources)
            });
        }
    }

    public Task<DesktopStateChangedEvent> SetLocaleAsync(string locale)
    {
        DesktopStateChangedEvent state;
        lock (_syncRoot)
        {
            _currentLocale = NormalizeLocale(locale);
            state = Snapshot();
        }

        StateChanged?.Invoke(this, state);
        return Task.FromResult(state);
    }

    public Task<DesktopSessionDetail?> GetSessionAsync(string sessionId) =>
        Task.FromResult(desktopSessionCatalogService.GetSession(_options.Sources, sessionId));

    public Task<QwenNativeToolExecutionResult> ExecuteNativeToolAsync(ExecuteNativeToolRequest request) =>
        qwenNativeToolHostService.ExecuteAsync(_options.Sources, request);

    public Task<DesktopSessionTurnResult> StartSessionTurnAsync(StartDesktopSessionTurnRequest request) =>
        desktopSessionHostService.StartTurnAsync(_options.Sources, request);

    public Task<DesktopSessionTurnResult> ApprovePendingToolAsync(ApproveDesktopSessionToolRequest request) =>
        desktopSessionHostService.ApprovePendingToolAsync(_options.Sources, request);

    private DesktopStateChangedEvent Snapshot() => new()
    {
        CurrentMode = DesktopMode.Code,
        CurrentLocale = _currentLocale,
        TimestampUtc = DateTime.UtcNow
    };

    private static string NormalizeLocale(string locale)
    {
        var exact = SupportedLocales.FirstOrDefault(item =>
            string.Equals(item.Code, locale, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Code;
        }

        var language = locale.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        var fallback = SupportedLocales.FirstOrDefault(item =>
            string.Equals(
                item.Code.Split('-', StringSplitOptions.RemoveEmptyEntries)[0],
                language,
                StringComparison.OrdinalIgnoreCase));

        return fallback?.Code ?? "en";
    }
}
