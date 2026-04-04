using QwenCode.App.Compatibility;
using QwenCode.App.Models;

namespace QwenCode.App.Infrastructure;

public sealed class GitWorktreeService(
    IGitCliService gitCliService,
    QwenRuntimeProfileService runtimeProfileService) : IGitWorktreeService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public GitRepositorySnapshot CreateManagedWorktree(WorkspacePaths paths, CreateManagedWorktreeRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        ValidateRequest(request);

        var inspection = Inspect(paths);
        if (!inspection.IsGitAvailable)
        {
            throw new InvalidOperationException("Git is not available.");
        }

        if (!inspection.IsRepository)
        {
            throw new InvalidOperationException("Workspace is not inside a git repository.");
        }

        if (!inspection.WorktreeSupported)
        {
            throw new InvalidOperationException("Git worktree support is not available.");
        }

        var sessionId = SanitizePathSegment(request.SessionId);
        var sanitizedName = SanitizePathSegment(request.Name);
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(sanitizedName))
        {
            throw new InvalidOperationException("Session and worktree names must remain non-empty after sanitization.");
        }

        var baseBranch = !string.IsNullOrWhiteSpace(request.BaseBranch)
            ? request.BaseBranch.Trim()
            : inspection.CurrentBranch;
        if (string.IsNullOrWhiteSpace(baseBranch))
        {
            throw new InvalidOperationException("Unable to resolve a base branch for the worktree.");
        }

        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "worktrees", sessionId);
        var worktreesDirectory = Path.Combine(sessionDirectory, "worktrees");
        Directory.CreateDirectory(worktreesDirectory);

        var worktreePath = Path.Combine(worktreesDirectory, sanitizedName);
        if (Directory.Exists(worktreePath))
        {
            throw new InvalidOperationException($"Worktree already exists at {worktreePath}.");
        }

        var shortSessionId = sessionId.Length > 6 ? sessionId[..6] : sessionId;
        var branchName = $"{baseBranch}-{shortSessionId}-{sanitizedName}";
        var createResult = gitCliService.Run(
            runtimeProfile.ProjectRoot,
            "worktree",
            "add",
            "-b",
            branchName,
            worktreePath,
            baseBranch);
        if (!createResult.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(createResult.StandardError)
                    ? "Failed to create managed worktree."
                    : createResult.StandardError.Trim());
        }

        WriteSessionConfig(sessionDirectory, runtimeProfile.ProjectRoot, request, sanitizedName, baseBranch);
        return Inspect(paths);
    }

    public GitRepositorySnapshot CleanupManagedSession(WorkspacePaths paths, CleanupManagedWorktreeSessionRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var inspection = Inspect(paths);
        var sessionId = SanitizePathSegment(request.SessionId);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("Session id is required.");
        }

        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "worktrees", sessionId);
        if (!Directory.Exists(sessionDirectory))
        {
            return inspection;
        }

        var managedWorktrees = inspection.Worktrees
            .Where(item => item.IsManaged && string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var worktree in managedWorktrees)
        {
            var removeResult = gitCliService.Run(runtimeProfile.ProjectRoot, "worktree", "remove", "--force", worktree.Path);
            if (!removeResult.Success && Directory.Exists(worktree.Path))
            {
                Directory.Delete(worktree.Path, recursive: true);
            }

            if (!string.IsNullOrWhiteSpace(worktree.Branch))
            {
                _ = gitCliService.Run(runtimeProfile.ProjectRoot, "branch", "-D", worktree.Branch);
            }
        }

        _ = gitCliService.Run(runtimeProfile.ProjectRoot, "worktree", "prune");

        if (Directory.Exists(sessionDirectory))
        {
            Directory.Delete(sessionDirectory, recursive: true);
        }

        return Inspect(paths);
    }

    public ApplyWorktreeChangesResult ApplyWorktreeChanges(string sourceRepositoryPath, string worktreePath)
    {
        if (!Directory.Exists(sourceRepositoryPath))
        {
            throw new InvalidOperationException($"Source repository path does not exist: {sourceRepositoryPath}");
        }

        if (!Directory.Exists(worktreePath))
        {
            throw new InvalidOperationException($"Worktree path does not exist: {worktreePath}");
        }

        var normalizedSource = Path.GetFullPath(sourceRepositoryPath);
        var normalizedWorktree = Path.GetFullPath(worktreePath);
        var appliedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deletedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var nameStatusResult = gitCliService.Run(normalizedWorktree, "diff", "--name-status", "--find-renames");
        if (!nameStatusResult.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(nameStatusResult.StandardError)
                    ? "Failed to read worktree diff."
                    : nameStatusResult.StandardError.Trim());
        }

        foreach (var change in ParseNameStatus(nameStatusResult.StandardOutput))
        {
            switch (change.Kind)
            {
                case "delete":
                    DeleteTarget(normalizedSource, change.TargetPath, deletedFiles);
                    break;
                case "rename":
                    if (!string.IsNullOrWhiteSpace(change.SourcePath) &&
                        !string.Equals(change.SourcePath, change.TargetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        DeleteTarget(normalizedSource, change.SourcePath, deletedFiles);
                    }

                    CopyTarget(normalizedWorktree, normalizedSource, change.TargetPath, appliedFiles);
                    break;
                default:
                    CopyTarget(normalizedWorktree, normalizedSource, change.TargetPath, appliedFiles);
                    break;
            }
        }

        var untrackedResult = gitCliService.Run(normalizedWorktree, "ls-files", "--others", "--exclude-standard");
        if (untrackedResult.Success)
        {
            foreach (var relativePath in untrackedResult.StandardOutput
                         .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                CopyTarget(normalizedWorktree, normalizedSource, relativePath, appliedFiles);
            }
        }

        return new ApplyWorktreeChangesResult
        {
            SourceRepositoryPath = normalizedSource,
            WorktreePath = normalizedWorktree,
            AppliedFiles = appliedFiles.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
            DeletedFiles = deletedFiles.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    public GitRepositorySnapshot Inspect(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var managedWorktreesRoot = Path.Combine(runtimeProfile.GlobalQwenDirectory, "worktrees");
        var gitVersionResult = gitCliService.Run(runtimeProfile.ProjectRoot, "--version");
        var isGitAvailable = gitVersionResult.Success;
        var repositoryRootResult = isGitAvailable
            ? gitCliService.Run(runtimeProfile.ProjectRoot, "rev-parse", "--show-toplevel")
            : new GitCommandResult { Success = false, ExitCode = -1 };
        var isRepository = repositoryRootResult.Success;
        var repositoryRoot = isRepository
            ? NormalizeDirectory(repositoryRootResult.StandardOutput)
            : string.Empty;
        var currentBranch = isRepository
            ? NormalizeText(gitCliService.Run(runtimeProfile.ProjectRoot, "rev-parse", "--abbrev-ref", "HEAD").StandardOutput)
            : string.Empty;
        var currentCommit = isRepository
            ? NormalizeText(gitCliService.Run(runtimeProfile.ProjectRoot, "rev-parse", "HEAD").StandardOutput)
            : string.Empty;
        var worktreeResult = isRepository
            ? gitCliService.Run(runtimeProfile.ProjectRoot, "worktree", "list", "--porcelain")
            : new GitCommandResult { Success = false, ExitCode = -1 };

        return new GitRepositorySnapshot
        {
            IsGitAvailable = isGitAvailable,
            IsRepository = isRepository,
            WorktreeSupported = worktreeResult.Success,
            RepositoryRoot = repositoryRoot,
            CurrentBranch = currentBranch,
            CurrentCommit = currentCommit,
            GitVersion = NormalizeText(gitVersionResult.StandardOutput),
            ManagedSessionCount = CountManagedSessions(managedWorktreesRoot),
            ManagedWorktreesRoot = managedWorktreesRoot,
            Worktrees = worktreeResult.Success
                ? ParseWorktrees(worktreeResult.StandardOutput, runtimeProfile.ProjectRoot, managedWorktreesRoot)
                : []
        };
    }

    private static IReadOnlyList<GitWorktreeEntry> ParseWorktrees(
        string stdout,
        string currentWorkspaceRoot,
        string managedWorktreesRoot)
    {
        var normalizedCurrent = Path.GetFullPath(currentWorkspaceRoot);
        var entries = new List<GitWorktreeEntry>();
        string currentPath = string.Empty;
        string currentBranch = string.Empty;
        string currentHead = string.Empty;

        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                TryAddCurrentEntry();
                currentPath = string.Empty;
                currentBranch = string.Empty;
                currentHead = string.Empty;
                continue;
            }

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                currentPath = NormalizeDirectory(line["worktree ".Length..]);
                continue;
            }

            if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                currentBranch = line["branch ".Length..]
                    .Replace("refs/heads/", string.Empty, StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                currentHead = line["HEAD ".Length..];
            }
        }

        TryAddCurrentEntry();
        return entries;

        void TryAddCurrentEntry()
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return;
            }

            var managedInfo = TryResolveManagedInfo(currentPath, managedWorktreesRoot);
            entries.Add(new GitWorktreeEntry
            {
                Path = currentPath,
                Branch = currentBranch,
                Head = currentHead,
                Name = Path.GetFileName(currentPath),
                SessionId = managedInfo.SessionId,
                IsCurrent = PathComparer.Equals(normalizedCurrent, currentPath),
                IsManaged = managedInfo.IsManaged
            });
        }
    }

    private static (bool IsManaged, string SessionId) TryResolveManagedInfo(string worktreePath, string managedWorktreesRoot)
    {
        if (!Directory.Exists(managedWorktreesRoot))
        {
            return (false, string.Empty);
        }

        var relativePath = TryGetRelativePath(managedWorktreesRoot, worktreePath);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return (false, string.Empty);
        }

        var segments = relativePath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 3 || !string.Equals(segments[1], "worktrees", StringComparison.OrdinalIgnoreCase))
        {
            return (false, string.Empty);
        }

        return (true, segments[0]);
    }

    private static string TryGetRelativePath(string root, string fullPath)
    {
        try
        {
            var relativePath = Path.GetRelativePath(root, fullPath);
            return relativePath.StartsWith("..", StringComparison.Ordinal) ? string.Empty : relativePath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int CountManagedSessions(string managedWorktreesRoot) =>
        Directory.Exists(managedWorktreesRoot)
            ? Directory.EnumerateDirectories(managedWorktreesRoot).Count()
            : 0;

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path.Trim());

    private static void ValidateRequest(CreateManagedWorktreeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            throw new InvalidOperationException("Session id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Worktree name is required.");
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select(character => invalidCharacters.Contains(character) || char.IsWhiteSpace(character) ? '-' : character)
            .ToArray());
        sanitized = sanitized.Trim('-');
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        return sanitized;
    }

    private static void WriteSessionConfig(
        string sessionDirectory,
        string sourceRepositoryPath,
        CreateManagedWorktreeRequest request,
        string sanitizedName,
        string baseBranch)
    {
        var configPath = Path.Combine(sessionDirectory, "config.json");
        var config = new
        {
            sessionId = request.SessionId,
            sourceRepoPath = sourceRepositoryPath,
            worktreeNames = ReadExistingWorktreeNames(configPath)
                .Append(sanitizedName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            baseBranch,
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        File.WriteAllText(
            configPath,
            System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }

    private static IReadOnlyList<GitNameStatusChange> ParseNameStatus(string stdout)
    {
        var changes = new List<GitNameStatusChange>();
        foreach (var rawLine in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = rawLine.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var statusCode = parts[0];
            var kind = statusCode[0] switch
            {
                'D' => "delete",
                'R' => "rename",
                _ => "copy"
            };

            changes.Add(kind switch
            {
                "rename" when parts.Length >= 3 => new GitNameStatusChange(kind, parts[1], parts[2]),
                "delete" => new GitNameStatusChange(kind, parts[1], parts[1]),
                _ => new GitNameStatusChange(kind, string.Empty, parts[1])
            });
        }

        return changes;
    }

    private static void CopyTarget(
        string worktreeRoot,
        string sourceRoot,
        string relativePath,
        ISet<string> appliedFiles)
    {
        var sourcePath = Path.GetFullPath(Path.Combine(worktreeRoot, relativePath));
        if (!PathStartsWith(sourcePath, worktreeRoot))
        {
            throw new InvalidOperationException("Worktree file path escaped the worktree root.");
        }

        if (!File.Exists(sourcePath))
        {
            return;
        }

        var targetPath = Path.GetFullPath(Path.Combine(sourceRoot, relativePath));
        if (!PathStartsWith(targetPath, sourceRoot))
        {
            throw new InvalidOperationException("Applied file path escaped the source repository root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
        appliedFiles.Add(targetPath);
    }

    private static void DeleteTarget(string sourceRoot, string relativePath, ISet<string> deletedFiles)
    {
        var targetPath = Path.GetFullPath(Path.Combine(sourceRoot, relativePath));
        if (!PathStartsWith(targetPath, sourceRoot))
        {
            throw new InvalidOperationException("Deleted file path escaped the source repository root.");
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
            deletedFiles.Add(targetPath);
        }
    }

    private static IReadOnlyList<string> ReadExistingWorktreeNames(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(configPath);
            using var document = System.Text.Json.JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("worktreeNames", out var property) ||
                property.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return [];
            }

            return property.EnumerateArray()
                .Where(static item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(item => item.GetString())
                .OfType<string>()
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static bool PathStartsWith(string path, string root) =>
        path.StartsWith(
            root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
        string.Equals(path, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record GitNameStatusChange(string Kind, string SourcePath, string TargetPath);
}
