using QwenCode.Core.Compatibility;
using QwenCode.Core.Models;

namespace QwenCode.Core.Infrastructure;

/// <summary>
/// Represents the Git History Service
/// </summary>
/// <param name="gitCliService">The git cli service</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
public sealed class GitHistoryService(
    IGitCliService gitCliService,
    QwenRuntimeProfileService runtimeProfileService) : IGitHistoryService
{
    private const string DefaultCommitMessagePrefix = "Desktop checkpoint";

    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting git history snapshot</returns>
    public GitHistorySnapshot Inspect(WorkspacePaths paths)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var historyDirectory = runtimeProfile.HistoryDirectory;
        var gitDirectory = Path.Combine(historyDirectory, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            return CreateEmptySnapshot(historyDirectory);
        }

        var headResult = RunHistoryGit(runtimeProfile, "rev-parse", "HEAD");
        var countResult = RunHistoryGit(runtimeProfile, "rev-list", "--count", "HEAD");
        var logResult = RunHistoryGit(runtimeProfile, "log", "--max-count", "5", "--date=iso-strict", "--pretty=format:%H%x1f%cI%x1f%s");

        return new GitHistorySnapshot
        {
            IsInitialized = true,
            HistoryDirectory = historyDirectory,
            CheckpointCount = ParseCount(countResult.StandardOutput),
            CurrentCheckpoint = headResult.Success ? headResult.StandardOutput.Trim() : string.Empty,
            RecentCheckpoints = logResult.Success
                ? ParseCheckpoints(logResult.StandardOutput)
                : []
        };
    }

    /// <summary>
    /// Creates checkpoint
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting git history snapshot</returns>
    public GitHistorySnapshot CreateCheckpoint(WorkspacePaths paths, CreateGitCheckpointRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        EnsureInitialized(runtimeProfile);

        var addResult = RunHistoryGit(runtimeProfile, "add", "-A", ".");
        if (!addResult.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(addResult.StandardError)
                ? "Failed to stage files in the git history store."
                : addResult.StandardError.Trim());
        }

        var commitMessage = string.IsNullOrWhiteSpace(request.Message)
            ? $"{DefaultCommitMessagePrefix} {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
            : request.Message.Trim();
        var commitResult = RunHistoryGit(runtimeProfile, "commit", "--allow-empty", "-m", commitMessage);
        if (!commitResult.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(commitResult.StandardError)
                ? "Failed to create a git history checkpoint."
                : commitResult.StandardError.Trim());
        }

        return Inspect(paths);
    }

    /// <summary>
    /// Restores checkpoint
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting git history snapshot</returns>
    public GitHistorySnapshot RestoreCheckpoint(WorkspacePaths paths, RestoreGitCheckpointRequest request)
    {
        var runtimeProfile = runtimeProfileService.Inspect(paths);
        var commitHash = request.CommitHash.Trim();
        if (string.IsNullOrWhiteSpace(commitHash))
        {
            throw new InvalidOperationException("A checkpoint commit hash is required.");
        }

        var gitDirectory = Path.Combine(runtimeProfile.HistoryDirectory, ".git");
        if (!Directory.Exists(gitDirectory))
        {
            throw new InvalidOperationException("The git history store has not been initialized yet.");
        }

        var restoreResult = RunHistoryGit(runtimeProfile, "restore", "--source", commitHash, ".");
        if (!restoreResult.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(restoreResult.StandardError)
                ? "Failed to restore the workspace from the selected checkpoint."
                : restoreResult.StandardError.Trim());
        }

        var cleanResult = RunHistoryGit(runtimeProfile, "clean", "-f", "-d");
        if (!cleanResult.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(cleanResult.StandardError)
                ? "Failed to remove untracked files after restoring the selected checkpoint."
                : cleanResult.StandardError.Trim());
        }

        return Inspect(paths);
    }

    private void EnsureInitialized(QwenRuntimeProfile runtimeProfile)
    {
        var historyDirectory = runtimeProfile.HistoryDirectory;
        Directory.CreateDirectory(historyDirectory);
        var gitDirectory = Path.Combine(historyDirectory, ".git");
        if (Directory.Exists(gitDirectory))
        {
            return;
        }

        var initResult = gitCliService.Run(historyDirectory, "init", "--initial-branch=main");
        if (!initResult.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(initResult.StandardError)
                ? "Failed to initialize the git history store."
                : initResult.StandardError.Trim());
        }

        _ = gitCliService.Run(historyDirectory, "config", "user.name", "Qwen Code");
        _ = gitCliService.Run(historyDirectory, "config", "user.email", "qwen-code@qwen.ai");
        _ = gitCliService.Run(historyDirectory, "config", "commit.gpgsign", "false");
        _ = gitCliService.Run(historyDirectory, "config", "core.autocrlf", "false");

        var initialCommitResult = RunHistoryGit(runtimeProfile, "commit", "--allow-empty", "-m", "Initial commit");
        if (!initialCommitResult.Success)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(initialCommitResult.StandardError)
                ? "Failed to create the initial git history checkpoint."
                : initialCommitResult.StandardError.Trim());
        }
    }

    private GitCommandResult RunHistoryGit(QwenRuntimeProfile runtimeProfile, params string[] arguments)
    {
        var historyGitDirectory = Path.Combine(runtimeProfile.HistoryDirectory, ".git");
        var effectiveArguments = new List<string>
        {
            $"--git-dir={historyGitDirectory}",
            $"--work-tree={runtimeProfile.ProjectRoot}"
        };
        effectiveArguments.AddRange(arguments);
        return gitCliService.Run(runtimeProfile.ProjectRoot, effectiveArguments.ToArray());
    }

    private static GitHistorySnapshot CreateEmptySnapshot(string historyDirectory) =>
        new()
        {
            IsInitialized = false,
            HistoryDirectory = historyDirectory,
            CheckpointCount = 0,
            CurrentCheckpoint = string.Empty,
            RecentCheckpoints = []
        };

    private static int ParseCount(string rawValue) =>
        int.TryParse(rawValue.Trim(), out var result) ? result : 0;

    private static IReadOnlyList<GitCheckpointSnapshot> ParseCheckpoints(string rawValue) =>
        rawValue
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Split('\u001f'))
            .Where(static parts => parts.Length >= 3)
            .Select(static parts => new GitCheckpointSnapshot
            {
                CommitHash = parts[0],
                CreatedAt = parts[1],
                Message = parts[2]
            })
            .ToArray();
}
