using QwenCode.App.Models;

namespace QwenCode.App.Desktop.Diagnostics;

public sealed class SourceMirrorInspectorService
{
    public IReadOnlyList<SourceMirrorStatus> Inspect(SourceMirrorPaths paths) =>
    [
        InspectPath(
            "workspace",
            "Desktop workspace",
            paths.WorkspaceRoot,
            ["QwenCode.slnx", "QwenCode.App/QwenCode.App.csproj"],
            "Electron host, IPC generator, and renderer workspace."),
        InspectPath(
            "qwen",
            "qwen-code",
            paths.QwenRoot,
            ["package.json", "packages/core", "docs/developers/architecture.md"],
            "Primary runtime and tool execution reference."),
        InspectPath(
            "claude",
            "claude-code",
            paths.ClaudeRoot,
            ["src/bridge/types.ts", "src/commands/desktop/desktop.tsx"],
            "Desktop UX and session bridge reference."),
        InspectPath(
            "ipc",
            "IPC reference",
            paths.IpcReferenceRoot,
            [],
            "Typed preload and shell integration reference.")
    ];

    private static SourceMirrorStatus InspectPath(
        string id,
        string title,
        string path,
        IReadOnlyList<string> markers,
        string purpose)
    {
        var hasPath = !string.IsNullOrWhiteSpace(path);
        var exists = hasPath && Directory.Exists(path);
        var isGitRepository = exists && Directory.Exists(Path.Combine(path, ".git"));
        var primaryMarker = exists
            ? markers.FirstOrDefault(marker => Exists(Path.Combine(path, marker)))
            : null;

        var status = !hasPath || !exists
            ? "missing"
            : isGitRepository && primaryMarker is not null
                ? "ready"
                : "partial";

        var summary = status switch
        {
            "ready" => $"{purpose} Repository and expected markers are available.",
            "partial" => $"{purpose} Path exists, but some expected repository markers are missing.",
            _ => $"{purpose} Configure a valid path to enable source inspection."
        };

        var highlights = BuildHighlights(isGitRepository, exists, primaryMarker, markers);

        return new SourceMirrorStatus
        {
            Id = id,
            Title = title,
            Path = path,
            Status = status,
            Summary = summary,
            Exists = exists,
            IsGitRepository = isGitRepository,
            PrimaryMarker = primaryMarker?.Replace('\\', '/'),
            Highlights = highlights
        };
    }

    private static IReadOnlyList<string> BuildHighlights(
        bool isGitRepository,
        bool exists,
        string? primaryMarker,
        IReadOnlyList<string> markers)
    {
        var highlights = new List<string>();

        if (exists)
        {
            highlights.Add("Directory found");
        }

        if (isGitRepository)
        {
            highlights.Add("Git repository detected");
        }

        if (primaryMarker is not null)
        {
            highlights.Add($"Primary marker: {primaryMarker.Replace('\\', '/')}");
        }
        else if (markers.Count > 0)
        {
            highlights.Add($"Expected marker: {markers[0].Replace('\\', '/')}");
        }

        return highlights;
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
