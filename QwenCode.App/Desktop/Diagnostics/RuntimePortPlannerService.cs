using System.Text.Json;
using QwenCode.App.Models;

namespace QwenCode.App.Desktop.Diagnostics;

public sealed class RuntimePortPlannerService
{
    public IReadOnlyList<RuntimePortWorkItem> BuildPlan(SourceMirrorPaths paths)
    {
        var qwenInfo = InspectQwenRepository(paths.QwenRoot);
        var claudeInfo = InspectClaudeRepository(paths.ClaudeRoot);

        return
        [
            CreateQwenCoreEngineItem(qwenInfo),
            CreateQwenToolingItem(qwenInfo),
            CreateQwenCompatibilityItem(qwenInfo),
            CreateClaudeSessionHostItem(claudeInfo),
            CreateClaudeApprovalUxItem(claudeInfo),
            CreateClaudeWorkspaceUxItem(claudeInfo)
        ];
    }

    private static RuntimePortWorkItem CreateQwenCoreEngineItem(RepositoryInspection qwenInfo) => new()
    {
        Id = "qwen-core-engine",
        Title = "Port qwen core engine to .NET runtime services",
        SourceSystem = "qwen-code",
        TargetModule = "QwenCode.Runtime",
        Stage = qwenInfo.IsReady ? "next" : "blocked",
        Summary = qwenInfo.IsReady
            ? $"Mirror is ready: qwen-code {qwenInfo.VersionLabel} with {qwenInfo.WorkspaceCount} workspace entries and a detected core package."
            : "qwen-code mirror is incomplete, so the engine port cannot be grounded in source markers yet.",
        CompatibilityContract = "Preserve prompt assembly, session turns, model/tool orchestration, and history semantics without routing through qwen CLI.",
        EvidencePaths = BuildEvidence(
            qwenInfo.Root,
            "package.json",
            "packages/core",
            "docs/developers/architecture.md")
    };

    private static RuntimePortWorkItem CreateQwenToolingItem(RepositoryInspection qwenInfo) => new()
    {
        Id = "qwen-tooling-host",
        Title = "Rebuild qwen tool registry as native .NET services",
        SourceSystem = "qwen-code",
        TargetModule = "QwenCode.Runtime.Tools",
        Stage = qwenInfo.HasTools ? "next" : "blocked",
        Summary = qwenInfo.HasTools
            ? $"Tool sources are present under packages/core/src/tools with {qwenInfo.ToolMarkerCount} key markers detected for shell, file, search, and MCP work."
            : "Tool registry markers are missing, so approval and tool-hosting behavior cannot be mapped reliably.",
        CompatibilityContract = "Keep qwen tool names, approval boundaries, and workspace behavior stable while swapping the execution host to .NET.",
        EvidencePaths = BuildEvidence(
            qwenInfo.Root,
            "packages/core/src/tools",
            "packages/core/src/tools/tool-registry.ts",
            "packages/core/src/tools/shell.ts",
            "packages/core/src/tools/read-file.ts")
    };

    private static RuntimePortWorkItem CreateQwenCompatibilityItem(RepositoryInspection qwenInfo) => new()
    {
        Id = "qwen-compat-settings",
        Title = "Preserve qwen settings, skills, and command compatibility",
        SourceSystem = "qwen-code",
        TargetModule = "QwenCode.Runtime.Configuration",
        Stage = qwenInfo.HasCompatibilitySurface ? "next" : "blocked",
        Summary = qwenInfo.HasCompatibilitySurface
            ? "Compatibility markers for .qwen commands, skills, and documented settings are present and ready to be modeled in .NET."
            : "Compatibility markers are incomplete, so .qwen surface parity would be speculative.",
        CompatibilityContract = "Do not fork .qwen conventions; instead read and honor compatible settings, commands, and skill locations from the native runtime.",
        EvidencePaths = BuildEvidence(
            qwenInfo.Root,
            ".qwen",
            ".qwen/commands",
            ".qwen/skills",
            "docs/users/configuration/settings.md")
    };

    private static RuntimePortWorkItem CreateClaudeSessionHostItem(RepositoryInspection claudeInfo) => new()
    {
        Id = "claude-session-host",
        Title = "Adapt Claude session bridge patterns for native desktop hosting",
        SourceSystem = "claude-code",
        TargetModule = "QwenCode.SessionHost",
        Stage = claudeInfo.HasBridge ? "next" : "blocked",
        Summary = claudeInfo.HasBridge
            ? $"Bridge sources are present with {claudeInfo.BridgeFileCount} bridge files, including typed session and transport contracts."
            : "Claude bridge markers are missing, so session-host adaptation has no reliable source inventory.",
        CompatibilityContract = "Adopt reconnectable session-host and activity-tracking patterns, but keep Qwen as the only model/runtime authority.",
        EvidencePaths = BuildEvidence(
            claudeInfo.Root,
            "src/bridge/types.ts",
            "src/bridge/sessionRunner.ts",
            "src/bridge/codeSessionApi.ts")
    };

    private static RuntimePortWorkItem CreateClaudeApprovalUxItem(RepositoryInspection claudeInfo) => new()
    {
        Id = "claude-approval-ux",
        Title = "Port explicit approval and permission UX into the renderer",
        SourceSystem = "claude-code",
        TargetModule = "QwenCode.Renderer.Approvals",
        Stage = claudeInfo.HasPermissionSurfaces ? "queued" : "blocked",
        Summary = claudeInfo.HasPermissionSurfaces
            ? "Permission- and session-oriented command surfaces are present and can be translated into desktop approval panels."
            : "Claude permission surfaces are missing, so approval UX cannot be adapted safely.",
        CompatibilityContract = "Renderer should expose approvals clearly, but the decision rules must still come from qwen-compatible runtime policy.",
        EvidencePaths = BuildEvidence(
            claudeInfo.Root,
            "src/bridge/bridgePermissionCallbacks.ts",
            "src/commands/permissions",
            "src/commands/plan")
    };

    private static RuntimePortWorkItem CreateClaudeWorkspaceUxItem(RepositoryInspection claudeInfo) => new()
    {
        Id = "claude-workspace-ux",
        Title = "Adapt Claude desktop workspace navigation to qwen desktop surfaces",
        SourceSystem = "claude-code",
        TargetModule = "QwenCode.Renderer.Workspace",
        Stage = claudeInfo.HasDesktopUx ? "foundation" : "blocked",
        Summary = claudeInfo.HasDesktopUx
            ? "Desktop, statusline, and session command surfaces are available as concrete UX references for chat, cowork, and code modes."
            : "Desktop navigation markers are missing, so workspace adaptation would be guesswork.",
        CompatibilityContract = "Borrow desktop navigation, session discovery, and activity presentation while keeping qwen storage and behavior compatible.",
        EvidencePaths = BuildEvidence(
            claudeInfo.Root,
            "src/commands/desktop/desktop.tsx",
            "src/commands/statusline.tsx",
            "src/commands/session")
    };

    private static RepositoryInspection InspectQwenRepository(string root)
    {
        var packageJsonPath = Path.Combine(root, "package.json");
        var versionLabel = "unknown";
        var workspaceCount = 0;

        if (File.Exists(packageJsonPath))
        {
            using var stream = File.OpenRead(packageJsonPath);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.TryGetProperty("version", out var version))
            {
                versionLabel = version.GetString() ?? "unknown";
            }

            if (document.RootElement.TryGetProperty("workspaces", out var workspaces) &&
                workspaces.ValueKind == JsonValueKind.Array)
            {
                workspaceCount = workspaces.GetArrayLength();
            }
        }

        return new RepositoryInspection(
            root,
            Directory.Exists(root),
            versionLabel,
            workspaceCount,
            CountExistingMarkers(
                root,
                "packages/core",
                "docs/developers/architecture.md"),
            CountExistingMarkers(
                root,
                "packages/core/src/tools",
                "packages/core/src/tools/tool-registry.ts",
                "packages/core/src/tools/shell.ts",
                "packages/core/src/tools/read-file.ts"),
            CountExistingMarkers(
                root,
                ".qwen",
                ".qwen/commands",
                ".qwen/skills",
                "docs/users/configuration/settings.md"),
            CountBridgeFiles(root));
    }

    private static RepositoryInspection InspectClaudeRepository(string root) =>
        new(
            root,
            Directory.Exists(root),
            "source",
            0,
            CountExistingMarkers(
                root,
                "src/bridge/types.ts",
                "src/bridge/sessionRunner.ts",
                "src/bridge/codeSessionApi.ts"),
            0,
            CountExistingMarkers(
                root,
                "src/bridge/bridgePermissionCallbacks.ts",
                "src/commands/permissions",
                "src/commands/plan"),
            CountBridgeFiles(root),
            CountExistingMarkers(
                root,
                "src/commands/desktop/desktop.tsx",
                "src/commands/statusline.tsx",
                "src/commands/session"));

    private static IReadOnlyList<string> BuildEvidence(string root, params string[] relativePaths) =>
        relativePaths
            .Select(path => Path.Combine(root, path))
            .Where(Exists)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToArray();

    private static int CountExistingMarkers(string root, params string[] relativePaths) =>
        relativePaths.Count(path => Exists(Path.Combine(root, path)));

    private static int CountBridgeFiles(string root)
    {
        var bridgePath = Path.Combine(root, "src", "bridge");
        return Directory.Exists(bridgePath)
            ? Directory.GetFiles(bridgePath, "*.ts").Length
            : 0;
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private sealed record RepositoryInspection(
        string Root,
        bool Exists,
        string VersionLabel,
        int WorkspaceCount,
        int CoreMarkerCount,
        int ToolMarkerCount,
        int CompatibilityMarkerCount,
        int BridgeFileCount,
        int DesktopMarkerCount = 0)
    {
        public bool IsReady => Exists && CoreMarkerCount >= 2;

        public bool HasTools => Exists && ToolMarkerCount >= 3;

        public bool HasCompatibilitySurface => Exists && CompatibilityMarkerCount >= 3;

        public bool HasBridge => Exists && BridgeFileCount > 0 && CoreMarkerCount >= 2;

        public bool HasPermissionSurfaces => Exists && CompatibilityMarkerCount >= 2;

        public bool HasDesktopUx => Exists && DesktopMarkerCount >= 2;
    }
}
