using QwenCode.Core.Models;

namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Represents the Workspace Path Resolver
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
public sealed class WorkspacePathResolver(IDesktopEnvironmentPaths environmentPaths) : IWorkspacePathResolver
{
    private static readonly string[] WorkspaceMarkers =
    [
        ".qwen",
        ".git",
        "QwenCode.sln",
        "QwenCode.slnx"
    ];

    /// <summary>
    /// Resolves value
    /// </summary>
    /// <param name="configured">The configured</param>
    /// <returns>The resulting workspace paths</returns>
    public WorkspacePaths Resolve(WorkspacePaths configured)
    {
        var workspaceRoot = ResolveWorkspaceRoot(configured.WorkspaceRoot);

        return new WorkspacePaths
        {
            WorkspaceRoot = workspaceRoot
        };
    }

    private string ResolveWorkspaceRoot(string configuredWorkspaceRoot)
    {
        var explicitRoot = ResolveExplicitPath("QWENCODE_WORKSPACE_ROOT", configuredWorkspaceRoot);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return explicitRoot;
        }

        var discoveredRoot =
            FindNearestAncestorWithMarkers(environmentPaths.CurrentDirectory, WorkspaceMarkers) ??
            FindNearestAncestorWithMarkers(environmentPaths.AppBaseDirectory, WorkspaceMarkers);

        return !string.IsNullOrWhiteSpace(discoveredRoot)
            ? discoveredRoot
            : NormalizeDirectory(environmentPaths.CurrentDirectory);
    }

    private string? ResolveExplicitPath(string environmentVariable, string configuredPath)
    {
        var environmentOverride = Environment.GetEnvironmentVariable(environmentVariable);
        if (TryNormalizeExistingDirectory(environmentOverride, out var environmentPath))
        {
            return environmentPath;
        }

        return TryNormalizeExistingDirectory(configuredPath, out var configuredDirectory)
            ? configuredDirectory
            : null;
    }

    private static string? FindNearestAncestorWithMarkers(
        string startDirectory,
        IReadOnlyList<string> markers)
    {
        if (!TryNormalizeExistingDirectory(startDirectory, out var normalizedStart))
        {
            return null;
        }

        var current = new DirectoryInfo(normalizedStart);
        while (current is not null)
        {
            if (HasAnyMarker(current.FullName, markers))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool HasAnyMarker(string directory, IReadOnlyList<string> markers) =>
        markers.Any(marker => File.Exists(Path.Combine(directory, marker)) || Directory.Exists(Path.Combine(directory, marker)));

    private static bool TryNormalizeExistingDirectory(string? path, out string normalizedDirectory)
    {
        normalizedDirectory = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = NormalizeDirectory(path);
            if (!Directory.Exists(fullPath))
            {
                return false;
            }

            normalizedDirectory = fullPath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeDirectory(string path) => Path.GetFullPath(path);
}
