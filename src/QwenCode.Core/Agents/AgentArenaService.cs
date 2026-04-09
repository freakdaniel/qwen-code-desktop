using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QwenCode.App.Compatibility;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;
using QwenCode.App.Runtime;
using QwenCode.App.Tools;

namespace QwenCode.App.Agents;

/// <summary>
/// Represents the Agent Arena Service
/// </summary>
/// <param name="gitWorktreeService">The git worktree service</param>
/// <param name="gitCliService">The git cli service</param>
/// <param name="runtimeProfileService">The runtime profile service</param>
/// <param name="serviceProvider">The service provider</param>
/// <param name="arenaSessionRegistry">The arena session registry</param>
public sealed class AgentArenaService(
    IGitWorktreeService gitWorktreeService,
    IGitCliService gitCliService,
    QwenRuntimeProfileService runtimeProfileService,
    IServiceProvider serviceProvider,
    IArenaSessionRegistry arenaSessionRegistry) : IAgentArenaService
{
    private const int MaxAgents = 5;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Executes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="arguments">The arguments</param>
    /// <param name="approvalState">The approval state</param>
    /// <param name="eventSink">The optional event sink</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to native tool execution result</returns>
    public async Task<NativeToolExecutionResult> ExecuteAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        Action<AssistantRuntimeEvent>? eventSink = null,
        CancellationToken cancellationToken = default)
    {
        var action = TryGetOptionalString(arguments, "action");
        if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadStatusAsync(runtimeProfile, arguments, approvalState, cancellationToken);
        }

        if (string.Equals(action, "cleanup", StringComparison.OrdinalIgnoreCase))
        {
            return CleanupArenaSession(paths, runtimeProfile, arguments, approvalState);
        }

        if (string.Equals(action, "discard", StringComparison.OrdinalIgnoreCase))
        {
            return await DiscardArenaSessionAsync(runtimeProfile, arguments, approvalState, cancellationToken);
        }

        if (string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase))
        {
            return CancelArenaSession(runtimeProfile, arguments, approvalState);
        }

        if (string.Equals(action, "select_winner", StringComparison.OrdinalIgnoreCase))
        {
            return await SelectWinnerAsync(runtimeProfile, arguments, approvalState, cancellationToken);
        }

        if (string.Equals(action, "apply_winner", StringComparison.OrdinalIgnoreCase))
        {
            return await ApplyWinnerAsync(runtimeProfile, arguments, approvalState, cancellationToken);
        }

        if (string.Equals(action, "follow_up", StringComparison.OrdinalIgnoreCase))
        {
            return await ContinueArenaSessionAsync(paths, runtimeProfile, arguments, approvalState, eventSink, cancellationToken);
        }

        if (!TryGetRequiredString(arguments, "task", out var task))
        {
            return Error("Parameter 'task' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        var models = ParseModels(arguments);
        if (models.Count < 2)
        {
            return Error("Arena requires at least two models.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (models.Count > MaxAgents)
        {
            return Error($"Arena supports up to {MaxAgents} models per session.", runtimeProfile.ProjectRoot, approvalState);
        }

        var cleanup = TryGetBool(arguments, "cleanup") ?? false;
        var explicitSessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        var taskId = TryGetOptionalString(arguments, "task_id") ?? TryGetOptionalString(arguments, "taskId") ?? string.Empty;
        var sessionId = string.IsNullOrWhiteSpace(explicitSessionId)
            ? $"arena-{Guid.NewGuid():N}"
            : SanitizePathSegment(explicitSessionId);
        var repository = gitWorktreeService.Inspect(paths);
        if (!repository.IsGitAvailable)
        {
            return Error("Git is not available.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!repository.IsRepository)
        {
            return Error("Workspace is not inside a git repository.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!repository.WorktreeSupported)
        {
            return Error("Git worktree support is not available.", runtimeProfile.ProjectRoot, approvalState);
        }

        var baseBranch = TryGetOptionalString(arguments, "base_branch");
        if (string.IsNullOrWhiteSpace(baseBranch))
        {
            baseBranch = repository.CurrentBranch;
        }

        if (string.IsNullOrWhiteSpace(baseBranch))
        {
            return Error("Unable to resolve a base branch for the arena session.", runtimeProfile.ProjectRoot, approvalState);
        }

        var allowedToolNames = ParseStringArray(arguments, "allowed_tools");
        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "arena", sessionId);
        var agentStatusesDirectory = Path.Combine(sessionDirectory, "agents");
        var transcriptsDirectory = Path.Combine(sessionDirectory, "transcripts");
        Directory.CreateDirectory(agentStatusesDirectory);
        Directory.CreateDirectory(transcriptsDirectory);
        var statusLock = new SemaphoreSlim(1, 1);

        var created = new List<CreatedArenaWorktree>();
        try
        {
            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "arena-initializing",
                Status = "running",
                Message = $"Preparing arena session '{sessionId}' with {models.Count} model competitors."
            });

            var createdAtUtc = DateTime.UtcNow;
            foreach (var model in models)
            {
                var worktreeName = BuildWorktreeName(model, created.Count);
                var snapshot = gitWorktreeService.CreateManagedWorktree(
                    paths,
                    new CreateManagedWorktreeRequest
                    {
                        SessionId = sessionId,
                        Name = worktreeName,
                        BaseBranch = baseBranch
                    });
                var worktree = snapshot.Worktrees.FirstOrDefault(item =>
                    item.IsManaged &&
                    string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.Name, worktreeName, StringComparison.OrdinalIgnoreCase));
                if (worktree is null)
                {
                    throw new InvalidOperationException($"Managed worktree '{worktreeName}' was created but not returned by git inspection.");
                }

                created.Add(new CreatedArenaWorktree(model, worktree));
            }

            if (!string.IsNullOrWhiteSpace(taskId))
            {
                await TryUpdateLinkedTaskAsync(runtimeProfile, taskId, "in_progress", $"arena:{sessionId}", cancellationToken);
            }

            return await ExecutePreparedArenaSessionAsync(
                runtimeProfile,
                runtimeProfile.ProjectRoot,
                task,
                taskId,
                sessionId,
                baseBranch,
                createdAtUtc,
                models,
                created,
                allowedToolNames,
                cleanup,
                1,
                string.Empty,
                approvalState,
                sessionDirectory,
                agentStatusesDirectory,
                transcriptsDirectory,
                statusLock,
                eventSink,
                cancellationToken);
        }
        catch (Exception exception)
        {
            return Error(exception.Message, runtimeProfile.ProjectRoot, approvalState);
        }
        finally
        {
            if (cleanup)
            {
                try
                {
                    gitWorktreeService.CleanupManagedSession(paths, new CleanupManagedWorktreeSessionRequest
                    {
                        SessionId = sessionId
                    });
                }
                catch
                {
                    // Best effort cleanup for managed arena worktrees.
                }
            }
        }
    }

    private async Task<NativeToolExecutionResult> ContinueArenaSessionAsync(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken)
    {
        var sessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error("Parameter 'session_id' is required for arena follow-up.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!TryGetRequiredString(arguments, "task", out var task))
        {
            return Error("Parameter 'task' must be a non-empty string.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sanitizedSessionId = SanitizePathSegment(sessionId);
        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "arena", sanitizedSessionId);
        var configPath = Path.Combine(sessionDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            return Error($"Arena session '{sanitizedSessionId}' was not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var config = JsonSerializer.Deserialize<ArenaSessionConfigFile>(
            await File.ReadAllTextAsync(configPath, cancellationToken));
        if (config is null)
        {
            return Error($"Arena session '{sanitizedSessionId}' has an invalid config.json.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (config.Models.Count < 2)
        {
            return Error($"Arena session '{sanitizedSessionId}' does not contain enough models for a follow-up round.", runtimeProfile.ProjectRoot, approvalState);
        }

        var inspection = gitWorktreeService.Inspect(paths);
        var created = BuildCreatedArenaWorktrees(config, inspection);
        if (created.Count != config.Models.Count)
        {
            return Error($"Arena session '{sanitizedSessionId}' is missing one or more managed worktrees required for follow-up.", runtimeProfile.ProjectRoot, approvalState);
        }

        var agentStatusesDirectory = Path.Combine(sessionDirectory, "agents");
        var transcriptsDirectory = Path.Combine(sessionDirectory, "transcripts");
        Directory.CreateDirectory(agentStatusesDirectory);
        Directory.CreateDirectory(transcriptsDirectory);
        var statusLock = new SemaphoreSlim(1, 1);
        var allowedToolNames = ParseStringArray(arguments, "allowed_tools");
        var taskId = TryGetOptionalString(arguments, "task_id") ?? TryGetOptionalString(arguments, "taskId") ?? config.TaskId;

        eventSink?.Invoke(new AssistantRuntimeEvent
        {
            Stage = "arena-follow-up",
            Status = "running",
            Message = $"Continuing arena session '{sanitizedSessionId}' with follow-up round {config.RoundCount + 1}."
        });

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            await TryUpdateLinkedTaskAsync(runtimeProfile, taskId, "in_progress", $"arena:{config.ArenaSessionId}", cancellationToken);
        }

        return await ExecutePreparedArenaSessionAsync(
            runtimeProfile,
            config.SourceRepoPath,
            task,
            taskId,
            config.ArenaSessionId,
            config.BaseBranch,
            config.CreatedAtUtc,
            config.Models,
            created,
            allowedToolNames,
            false,
            config.RoundCount + 1,
            config.SelectedWinner,
            approvalState,
            sessionDirectory,
            agentStatusesDirectory,
            transcriptsDirectory,
            statusLock,
            eventSink,
            cancellationToken);
    }

    private async Task<NativeToolExecutionResult> SelectWinnerAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var sessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error("Parameter 'session_id' is required for arena winner selection.", runtimeProfile.ProjectRoot, approvalState);
        }

        var winner = TryGetOptionalString(arguments, "winner") ?? TryGetOptionalString(arguments, "agent_name");
        if (string.IsNullOrWhiteSpace(winner))
        {
            return Error("Parameter 'winner' is required for arena winner selection.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sanitizedSessionId = SanitizePathSegment(sessionId);
        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "arena", sanitizedSessionId);
        var configPath = Path.Combine(sessionDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            return Error($"Arena session '{sanitizedSessionId}' was not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var config = JsonSerializer.Deserialize<ArenaSessionConfigFile>(
            await File.ReadAllTextAsync(configPath, cancellationToken));
        if (config is null)
        {
            return Error($"Arena session '{sanitizedSessionId}' has an invalid config.json.", runtimeProfile.ProjectRoot, approvalState);
        }

        var resolvedWinner = config.Agents.Keys.FirstOrDefault(name =>
            string.Equals(name, winner, StringComparison.OrdinalIgnoreCase));
        if (resolvedWinner is null)
        {
            return Error($"Arena session '{sanitizedSessionId}' does not contain agent '{winner}'.", runtimeProfile.ProjectRoot, approvalState);
        }

        var updatedConfig = new ArenaSessionConfigFile
        {
            ArenaSessionId = config.ArenaSessionId,
            SourceRepoPath = config.SourceRepoPath,
            Task = config.Task,
            TaskId = config.TaskId,
            RoundCount = config.RoundCount,
            SelectedWinner = resolvedWinner,
            AppliedWinner = config.AppliedWinner,
            Models = config.Models,
            WorktreeNames = config.WorktreeNames,
            BaseBranch = config.BaseBranch,
            CreatedAtUtc = config.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            Agents = config.Agents
        };

        var statusPath = Path.Combine(sessionDirectory, "status.json");
        var status = File.Exists(statusPath)
            ? JsonSerializer.Deserialize<ArenaSessionStatusFile>(await File.ReadAllTextAsync(statusPath, cancellationToken))
            : null;
        var updatedStatus = new ArenaSessionStatusFile
        {
            SessionId = updatedConfig.ArenaSessionId,
            Task = updatedConfig.Task,
            Status = status?.Status ?? "idle",
            BaseBranch = updatedConfig.BaseBranch,
            RoundCount = updatedConfig.RoundCount,
            SelectedWinner = resolvedWinner,
            AppliedWinner = status?.AppliedWinner ?? config.AppliedWinner,
            StartedAtUtc = status?.StartedAtUtc ?? updatedConfig.CreatedAtUtc,
            EndedAtUtc = status?.EndedAtUtc,
            Stats = status?.Stats ?? BuildArenaSessionStats(updatedConfig.Agents.Values.ToArray(), updatedConfig.CreatedAtUtc, DateTime.UtcNow, updatedConfig.RoundCount),
            Agents = status?.Agents ?? updatedConfig.Agents.Values.ToArray()
        };

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(updatedConfig, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(updatedStatus, JsonOptions), cancellationToken);

        arenaSessionRegistry.Update(
            sanitizedSessionId,
            state =>
            {
                state.SelectedWinner = resolvedWinner;
                state.RoundCount = updatedConfig.RoundCount;
            },
            ArenaSessionEventKind.SessionUpdated,
            $"Selected arena winner '{resolvedWinner}' for session '{sanitizedSessionId}'.");

        var resultPath = Path.Combine(sessionDirectory, "result.json");
        if (File.Exists(resultPath))
        {
            var result = JsonSerializer.Deserialize<ArenaSessionResult>(
                await File.ReadAllTextAsync(resultPath, cancellationToken));
            if (result is not null)
            {
                var updatedResult = new ArenaSessionResult
                {
                    SessionId = result.SessionId,
                    Task = result.Task,
                    TaskId = result.TaskId,
                    Status = result.Status,
                    BaseBranch = result.BaseBranch,
                    ArtifactPath = result.ArtifactPath,
                    RoundCount = result.RoundCount,
                    SelectedWinner = resolvedWinner,
                    AppliedWinner = result.AppliedWinner,
                    CleanupRequested = result.CleanupRequested,
                    StartedAtUtc = result.StartedAtUtc,
                    EndedAtUtc = result.EndedAtUtc,
                    Stats = CloneSessionStats(result.Stats),
                    Models = result.Models,
                    Agents = result.Agents
                };
                await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(updatedResult, JsonOptions), cancellationToken);
            }
        }

        return new NativeToolExecutionResult
        {
            ToolName = "arena",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Output = $"Selected arena winner '{resolvedWinner}' for session '{sanitizedSessionId}'.",
            ChangedFiles = []
        };
    }

    private async Task<NativeToolExecutionResult> ApplyWinnerAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var sessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error("Parameter 'session_id' is required for applying an arena winner.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sanitizedSessionId = SanitizePathSegment(sessionId);
        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "arena", sanitizedSessionId);
        var configPath = Path.Combine(sessionDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            return Error($"Arena session '{sanitizedSessionId}' was not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var config = JsonSerializer.Deserialize<ArenaSessionConfigFile>(
            await File.ReadAllTextAsync(configPath, cancellationToken));
        if (config is null)
        {
            return Error($"Arena session '{sanitizedSessionId}' has an invalid config.json.", runtimeProfile.ProjectRoot, approvalState);
        }

        var winner = TryGetOptionalString(arguments, "winner") ?? TryGetOptionalString(arguments, "agent_name") ?? config.SelectedWinner;
        if (string.IsNullOrWhiteSpace(winner))
        {
            return Error($"Arena session '{sanitizedSessionId}' does not have a selected winner yet.", runtimeProfile.ProjectRoot, approvalState);
        }

        var resolvedWinner = config.Agents.Keys.FirstOrDefault(name =>
            string.Equals(name, winner, StringComparison.OrdinalIgnoreCase));
        if (resolvedWinner is null)
        {
            return Error($"Arena session '{sanitizedSessionId}' does not contain agent '{winner}'.", runtimeProfile.ProjectRoot, approvalState);
        }

        if (!config.Agents.TryGetValue(resolvedWinner, out var winnerStatus))
        {
            return Error($"Arena session '{sanitizedSessionId}' does not contain persisted state for '{resolvedWinner}'.", runtimeProfile.ProjectRoot, approvalState);
        }

        var applyResult = gitWorktreeService.ApplyWorktreeChanges(config.SourceRepoPath, winnerStatus.WorktreePath);

        var updatedConfig = new ArenaSessionConfigFile
        {
            ArenaSessionId = config.ArenaSessionId,
            SourceRepoPath = config.SourceRepoPath,
            Task = config.Task,
            TaskId = config.TaskId,
            RoundCount = config.RoundCount,
            SelectedWinner = resolvedWinner,
            AppliedWinner = resolvedWinner,
            Models = config.Models,
            WorktreeNames = config.WorktreeNames,
            BaseBranch = config.BaseBranch,
            CreatedAtUtc = config.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            Agents = config.Agents
        };

        var statusPath = Path.Combine(sessionDirectory, "status.json");
        var status = File.Exists(statusPath)
            ? JsonSerializer.Deserialize<ArenaSessionStatusFile>(await File.ReadAllTextAsync(statusPath, cancellationToken))
            : null;
        var updatedStatus = new ArenaSessionStatusFile
        {
            SessionId = updatedConfig.ArenaSessionId,
            Task = updatedConfig.Task,
            Status = status?.Status ?? "completed",
            BaseBranch = updatedConfig.BaseBranch,
            RoundCount = updatedConfig.RoundCount,
            SelectedWinner = resolvedWinner,
            AppliedWinner = resolvedWinner,
            StartedAtUtc = status?.StartedAtUtc ?? updatedConfig.CreatedAtUtc,
            EndedAtUtc = status?.EndedAtUtc,
            Stats = status?.Stats ?? BuildArenaSessionStats(updatedConfig.Agents.Values.ToArray(), updatedConfig.CreatedAtUtc, DateTime.UtcNow, updatedConfig.RoundCount),
            Agents = status?.Agents ?? updatedConfig.Agents.Values.ToArray()
        };

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(updatedConfig, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(updatedStatus, JsonOptions), cancellationToken);

        var resultPath = Path.Combine(sessionDirectory, "result.json");
        if (File.Exists(resultPath))
        {
            var result = JsonSerializer.Deserialize<ArenaSessionResult>(
                await File.ReadAllTextAsync(resultPath, cancellationToken));
            if (result is not null)
            {
                var updatedResult = new ArenaSessionResult
                {
                    SessionId = result.SessionId,
                    Task = result.Task,
                    TaskId = result.TaskId,
                    Status = result.Status,
                    BaseBranch = result.BaseBranch,
                    ArtifactPath = result.ArtifactPath,
                    RoundCount = result.RoundCount,
                    SelectedWinner = resolvedWinner,
                    AppliedWinner = resolvedWinner,
                    CleanupRequested = result.CleanupRequested,
                    StartedAtUtc = result.StartedAtUtc,
                    EndedAtUtc = result.EndedAtUtc,
                    Stats = CloneSessionStats(result.Stats),
                    Models = result.Models,
                    Agents = result.Agents
                };
                await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(updatedResult, JsonOptions), cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(config.TaskId))
        {
            await TryUpdateLinkedTaskAsync(runtimeProfile, config.TaskId, "completed", resolvedWinner, cancellationToken);
        }

        var lines = new List<string>
        {
            $"Applied arena winner '{resolvedWinner}' from session '{sanitizedSessionId}'.",
            $"Applied files: {applyResult.AppliedFiles.Count}",
            $"Deleted files: {applyResult.DeletedFiles.Count}"
        };

        return new NativeToolExecutionResult
        {
            ToolName = "arena",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = config.SourceRepoPath,
            Output = string.Join(Environment.NewLine, lines),
            ChangedFiles = applyResult.AppliedFiles
                .Concat(applyResult.DeletedFiles)
                .Append(configPath)
                .Append(statusPath)
                .Append(resultPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private async Task<NativeToolExecutionResult> ExecutePreparedArenaSessionAsync(
        QwenRuntimeProfile runtimeProfile,
        string sourceRepoPath,
        string task,
        string taskId,
        string sessionId,
        string baseBranch,
        DateTime createdAtUtc,
        IReadOnlyList<ArenaModelDescriptor> models,
        IReadOnlyList<CreatedArenaWorktree> created,
        IReadOnlyList<string> allowedToolNames,
        bool cleanup,
        int roundCount,
        string selectedWinner,
        string approvalState,
        string sessionDirectory,
        string agentStatusesDirectory,
        string transcriptsDirectory,
        SemaphoreSlim statusLock,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken)
    {
        await WriteInitialSessionFilesAsync(
            sessionDirectory,
            sourceRepoPath,
            task,
            taskId,
            sessionId,
            baseBranch,
            createdAtUtc,
            created,
            models,
            roundCount,
            selectedWinner,
            statusLock,
            cancellationToken);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var initialAgentStates = created.Select(item => BuildAgentStatusFile(
                $"{sessionId}/{SanitizePathSegment(item.Model.AgentName)}",
                item,
                "initializing",
                "Arena competitor prepared.",
                string.Empty,
                string.Empty,
                DateTime.UtcNow))
            .ToArray();
        arenaSessionRegistry.Start(
            new ActiveArenaSessionState
            {
                SessionId = sessionId,
                Task = task,
                Status = "running",
                WorkingDirectory = sourceRepoPath,
                BaseBranch = baseBranch,
                RoundCount = roundCount,
                SelectedWinner = selectedWinner,
                StartedAtUtc = createdAtUtc,
                LastUpdatedAtUtc = DateTime.UtcNow,
                Stats = BuildArenaSessionStats(initialAgentStates, createdAtUtc, DateTime.UtcNow, roundCount),
                Agents = initialAgentStates
            },
            linkedCancellation,
            $"Arena session '{sessionId}' started for round {roundCount}.");

        try
        {
            var agentTasks = created
                .Select(item => ExecuteAgentAsync(
                    item,
                    task,
                    sessionId,
                    approvalState,
                    sessionDirectory,
                    agentStatusesDirectory,
                    transcriptsDirectory,
                    allowedToolNames,
                    statusLock,
                    createdAtUtc,
                    eventSink,
                    linkedCancellation.Token))
                .ToArray();
            var agentResults = await Task.WhenAll(agentTasks);
            var sessionStatus = agentResults.All(static item => string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase))
                ? "idle"
                : "failed";
            var artifactPath = Path.Combine(sessionDirectory, "result.json");
            var sessionResult = new ArenaSessionResult
            {
                SessionId = sessionId,
                Task = task,
                TaskId = taskId,
                Status = sessionStatus,
                BaseBranch = baseBranch,
                ArtifactPath = artifactPath,
                RoundCount = roundCount,
                SelectedWinner = selectedWinner,
                AppliedWinner = string.Empty,
                CleanupRequested = cleanup,
                StartedAtUtc = agentResults.Min(static item => item.StartedAtUtc),
                EndedAtUtc = agentResults.Max(static item => item.EndedAtUtc),
                Stats = BuildArenaSessionStats(
                    agentResults.Select(MapArenaAgentResultToStatusFile).ToArray(),
                    agentResults.Min(static item => item.StartedAtUtc),
                    agentResults.Max(static item => item.EndedAtUtc),
                    roundCount),
                Models = models,
                Agents = agentResults
            };

            await File.WriteAllTextAsync(
                artifactPath,
                JsonSerializer.Serialize(sessionResult, JsonOptions),
                cancellationToken);
            await WriteFinalSessionFilesAsync(
                sessionDirectory,
                sourceRepoPath,
                task,
                taskId,
                sessionResult,
                statusLock,
                cancellationToken);

            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "arena-completed",
                Status = sessionStatus,
                Message = $"Arena session '{sessionId}' finished round {roundCount} with status '{sessionStatus}'."
            });

            arenaSessionRegistry.Update(
                sessionId,
                state =>
                {
                    state.Status = sessionStatus;
                    state.RoundCount = roundCount;
                    state.SelectedWinner = selectedWinner;
                    state.Stats = CloneSessionStats(sessionResult.Stats);
                    state.Agents = sessionResult.Agents
                        .Select(MapArenaAgentResultToStatusFile)
                        .ToArray();
                },
                ArenaSessionEventKind.RoundCompleted,
                $"Arena session '{sessionId}' completed round {roundCount}.");
            arenaSessionRegistry.Complete(
                sessionId,
                sessionStatus,
                roundCount,
                selectedWinner,
                sessionResult.Stats,
                sessionResult.Agents.Select(MapArenaAgentResultToStatusFile).ToArray(),
                $"Arena session '{sessionId}' finished round {roundCount}.");

            var report = BuildArenaReport(sessionResult);
            var changedFiles = agentResults
                .Select(static item => item.TranscriptPath)
                .Append(artifactPath)
                .Append(Path.Combine(sessionDirectory, "config.json"))
                .Append(Path.Combine(sessionDirectory, "status.json"))
                .ToArray();

            return new NativeToolExecutionResult
            {
                ToolName = "arena",
                Status = sessionStatus == "failed" ? "error" : "completed",
                ApprovalState = approvalState,
                WorkingDirectory = sourceRepoPath,
                Output = report,
                ErrorMessage = sessionStatus == "failed" ? "One or more arena agents did not complete successfully." : string.Empty,
                ChangedFiles = changedFiles
            };
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            var cancelledAtUtc = DateTime.UtcNow;
            var activeSession = arenaSessionRegistry.ListActiveSessions()
                .FirstOrDefault(item => string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
            var cancelledAgentStatuses = activeSession?.Agents ?? created
                .Select(item => BuildAgentStatusFile(
                    $"{sessionId}/{SanitizePathSegment(item.Model.AgentName)}",
                    item,
                    "cancelled",
                    string.Empty,
                    string.Empty,
                    "Arena session was cancelled.",
                    cancelledAtUtc))
                .ToArray();
            var cancelledResult = BuildCancelledArenaSessionResult(
                sessionId,
                task,
                taskId,
                baseBranch,
                roundCount,
                activeSession?.SelectedWinner ?? selectedWinner,
                cleanup,
                sessionDirectory,
                transcriptsDirectory,
                created,
                cancelledAgentStatuses,
                createdAtUtc,
                cancelledAtUtc,
                models);

            await File.WriteAllTextAsync(
                cancelledResult.ArtifactPath,
                JsonSerializer.Serialize(cancelledResult, JsonOptions),
                cancellationToken);
            await WriteFinalSessionFilesAsync(
                sessionDirectory,
                sourceRepoPath,
                task,
                taskId,
                cancelledResult,
                statusLock,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(taskId))
            {
                await TryUpdateLinkedTaskAsync(runtimeProfile, taskId, "cancelled", $"arena:{sessionId}", cancellationToken);
            }

            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "arena-cancelled",
                Status = "cancelled",
                Message = $"Arena session '{sessionId}' was cancelled during round {roundCount}."
            });

            arenaSessionRegistry.Complete(
                sessionId,
                "cancelled",
                roundCount,
                cancelledResult.SelectedWinner,
                cancelledResult.Stats,
                cancelledAgentStatuses,
                $"Arena session '{sessionId}' was cancelled during round {roundCount}.");

            return new NativeToolExecutionResult
            {
                ToolName = "arena",
                Status = "cancelled",
                ApprovalState = approvalState,
                WorkingDirectory = sourceRepoPath,
                Output = BuildArenaReport(cancelledResult),
                ErrorMessage = "Arena session was cancelled.",
                ChangedFiles = cancelledResult.Agents
                    .Select(static item => item.TranscriptPath)
                    .Where(static path => !string.IsNullOrWhiteSpace(path))
                    .Append(cancelledResult.ArtifactPath)
                    .Append(Path.Combine(sessionDirectory, "config.json"))
                    .Append(Path.Combine(sessionDirectory, "status.json"))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        }
        catch
        {
            arenaSessionRegistry.Remove(sessionId, $"Arena session '{sessionId}' stopped unexpectedly.");
            throw;
        }
    }

    private async Task<ArenaAgentResult> ExecuteAgentAsync(
        CreatedArenaWorktree created,
        string task,
        string sessionId,
        string approvalState,
        string sessionDirectory,
        string agentStatusesDirectory,
        string transcriptsDirectory,
        IReadOnlyList<string> allowedToolNames,
        SemaphoreSlim statusLock,
        DateTime sessionStartedAtUtc,
        Action<AssistantRuntimeEvent>? eventSink,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var worktreePaths = new WorkspacePaths
        {
            WorkspaceRoot = created.Worktree.Path
        };
        var worktreeProfile = runtimeProfileService.Inspect(worktreePaths);
        var agentId = $"{sessionId}/{SanitizePathSegment(created.Model.AgentName)}";
        var transcriptPath = Path.Combine(transcriptsDirectory, $"{SanitizePathSegment(created.Model.AgentName)}.jsonl");
        await WriteAgentStatusAsync(
            sessionDirectory,
            agentStatusesDirectory,
            runtimeProfileService.Inspect(new WorkspacePaths { WorkspaceRoot = created.Worktree.Path }).ProjectRoot,
            task,
            sessionStartedAtUtc,
            BuildAgentStatusFile(
                agentId,
                created,
                "running",
                "Starting arena competitor.",
                string.Empty,
                string.Empty,
                DateTime.UtcNow),
            statusLock,
            cancellationToken);

        UpdateArenaAgentState(
            sessionId,
            created.Model.AgentName,
            ArenaSessionEventKind.AgentStarted,
            $"Arena agent '{created.Model.AgentName}' started.",
            existing => new ArenaAgentStatusFile
            {
                AgentId = existing.AgentId,
                AgentName = existing.AgentName,
                Status = "running",
                Model = existing.Model,
                StopReason = string.Empty,
                Stats = CloneStats(existing.Stats),
                WorktreeName = existing.WorktreeName,
                WorktreePath = existing.WorktreePath,
                Branch = existing.Branch,
                ProviderName = existing.ProviderName,
                CurrentActivity = "Starting arena competitor.",
                FinalSummary = string.Empty,
                Error = string.Empty,
                UpdatedAtUtc = DateTime.UtcNow
            });

        eventSink?.Invoke(new AssistantRuntimeEvent
        {
            Stage = "arena-agent-started",
            AgentName = created.Model.AgentName,
            ProviderName = "arena",
            Status = "running",
            Message = $"Arena agent '{created.Model.AgentName}' started in worktree '{created.Worktree.Name}'."
        });

        try
        {
            var request = new AssistantTurnRequest
            {
                SessionId = agentId,
                Prompt = BuildArenaTaskPrompt(task, created.Model),
                WorkingDirectory = created.Worktree.Path,
                TranscriptPath = transcriptPath,
                RuntimeProfile = worktreeProfile,
                GitBranch = created.Worktree.Branch,
                ToolExecution = new NativeToolExecutionResult
                {
                    ToolName = "arena",
                    Status = "not-requested",
                    ApprovalState = approvalState,
                    WorkingDirectory = created.Worktree.Path,
                    ChangedFiles = []
                },
                PromptMode = AssistantPromptMode.ArenaCompetitor,
                SystemPromptOverride = BuildArenaModeSpecificInstructions(created.Model, allowedToolNames),
                AllowedToolNames = allowedToolNames,
                ModelOverride = created.Model.Model,
                AuthTypeOverride = created.Model.AuthType,
                EndpointOverride = created.Model.BaseUrl,
                ApiKeyOverride = created.Model.ApiKey
            };

            var runtime = ResolveRuntime();
            var response = await runtime.GenerateAsync(
                request,
                runtimeEvent =>
                {
                    ApplyArenaRuntimeEventToAgentState(sessionId, created.Model.AgentName, runtimeEvent);
                    eventSink?.Invoke(CloneArenaEvent(created.Model.AgentName, runtimeEvent));
                },
                cancellationToken);
            await PersistTranscriptAsync(
                transcriptPath,
                agentId,
                created.Worktree.Path,
                request.Prompt,
                response,
                cancellationToken);

            var endedAtUtc = DateTime.UtcNow;
            var modifiedFiles = ReadModifiedFiles(created.Worktree.Path);
            var diff = ReadDiff(created.Worktree.Path);
            var resolvedStats = AssistantExecutionDiagnostics.ResolveStats(response, startedAtUtc, endedAtUtc);
            var resolvedStopReason = AssistantExecutionDiagnostics.ResolveStopReason(response);
            var agentStatus = ResolveArenaAgentStatus(response);
            var result = new ArenaAgentResult
            {
                AgentId = agentId,
                AgentName = created.Model.AgentName,
                Status = agentStatus,
                ProviderName = response.ProviderName,
                Model = response.Model,
                StopReason = resolvedStopReason,
                Stats = resolvedStats,
                WorktreePath = created.Worktree.Path,
                Branch = created.Worktree.Branch,
                TranscriptPath = transcriptPath,
                Summary = response.Summary,
                Diff = diff,
                ModifiedFiles = modifiedFiles,
                ToolExecutions = response.ToolExecutions
                    .Select(static item => new SubagentToolExecutionRecord
                    {
                        ToolName = item.Execution.ToolName,
                        Status = item.Execution.Status,
                        ApprovalState = item.Execution.ApprovalState,
                        ErrorMessage = item.Execution.ErrorMessage,
                        ChangedFiles = item.Execution.ChangedFiles
                    })
                    .ToArray(),
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc
            };
            await WriteAgentStatusAsync(
                sessionDirectory,
                agentStatusesDirectory,
                worktreeProfile.ProjectRoot,
                task,
                sessionStartedAtUtc,
                BuildAgentStatusFile(
                    agentId,
                    created,
                    result.Status,
                    string.Empty,
                    result.Summary,
                    string.Empty,
                    endedAtUtc,
                    response.ProviderName,
                    resolvedStopReason,
                    resolvedStats),
                statusLock,
                cancellationToken);

            UpdateArenaAgentState(
                sessionId,
                created.Model.AgentName,
                ArenaSessionEventKind.AgentCompleted,
                $"Arena agent '{created.Model.AgentName}' completed.",
                existing => new ArenaAgentStatusFile
                {
                    AgentId = existing.AgentId,
                    AgentName = existing.AgentName,
                    Status = result.Status,
                    Model = result.Model,
                    StopReason = result.StopReason,
                    Stats = CloneStats(result.Stats),
                    WorktreeName = existing.WorktreeName,
                    WorktreePath = existing.WorktreePath,
                    Branch = existing.Branch,
                    ProviderName = response.ProviderName,
                    CurrentActivity = string.Empty,
                    FinalSummary = result.Summary,
                    Error = string.Empty,
                    UpdatedAtUtc = endedAtUtc
                });

            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "arena-agent-completed",
                AgentName = created.Model.AgentName,
                ProviderName = response.ProviderName,
                Status = result.Status,
                Message = $"Arena agent '{created.Model.AgentName}' finished with status '{result.Status}' using model '{result.Model}'."
            });

            return result;
        }
        catch (Exception exception)
        {
            var endedAtUtc = DateTime.UtcNow;
            eventSink?.Invoke(new AssistantRuntimeEvent
            {
                Stage = "arena-agent-failed",
                AgentName = created.Model.AgentName,
                ProviderName = "arena",
                Status = "error",
                Message = $"Arena agent '{created.Model.AgentName}' failed: {exception.Message}"
            });
            await WriteAgentStatusAsync(
                sessionDirectory,
                agentStatusesDirectory,
                worktreeProfile.ProjectRoot,
                task,
                sessionStartedAtUtc,
                BuildAgentStatusFile(
                    agentId,
                    created,
                    "error",
                    string.Empty,
                    string.Empty,
                    exception.Message,
                    endedAtUtc,
                    stopReason: "runtime-error",
                    stats: AssistantExecutionDiagnostics.BuildStats(1, [], Math.Max(0L, (long)(endedAtUtc - startedAtUtc).TotalMilliseconds))),
                statusLock,
                cancellationToken);

            UpdateArenaAgentState(
                sessionId,
                created.Model.AgentName,
                ArenaSessionEventKind.AgentFailed,
                $"Arena agent '{created.Model.AgentName}' failed: {exception.Message}",
                existing => new ArenaAgentStatusFile
                {
                    AgentId = existing.AgentId,
                    AgentName = existing.AgentName,
                    Status = "error",
                    Model = existing.Model,
                    StopReason = "runtime-error",
                    Stats = AssistantExecutionDiagnostics.BuildStats(1, [], Math.Max(0L, (long)(endedAtUtc - startedAtUtc).TotalMilliseconds)),
                    WorktreeName = existing.WorktreeName,
                    WorktreePath = existing.WorktreePath,
                    Branch = existing.Branch,
                    ProviderName = existing.ProviderName,
                    CurrentActivity = string.Empty,
                    FinalSummary = string.Empty,
                    Error = exception.Message,
                    UpdatedAtUtc = endedAtUtc
                });
            return new ArenaAgentResult
            {
                AgentId = agentId,
                AgentName = created.Model.AgentName,
                Status = "error",
                ProviderName = "arena",
                Model = created.Model.Model,
                StopReason = "runtime-error",
                Stats = AssistantExecutionDiagnostics.BuildStats(1, [], Math.Max(0L, (long)(endedAtUtc - startedAtUtc).TotalMilliseconds)),
                WorktreePath = created.Worktree.Path,
                Branch = created.Worktree.Branch,
                TranscriptPath = transcriptPath,
                ErrorMessage = exception.Message,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc
            };
        }
    }

    private async Task<NativeToolExecutionResult> ReadStatusAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var sessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error("Parameter 'session_id' is required for arena status.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "arena", SanitizePathSegment(sessionId));
        var statusPath = Path.Combine(sessionDirectory, "status.json");
        var resultPath = Path.Combine(sessionDirectory, "result.json");
        var activeSession = arenaSessionRegistry.ListActiveSessions()
            .FirstOrDefault(item => string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        if (activeSession is not null)
        {
            return new NativeToolExecutionResult
            {
                ToolName = "arena",
                Status = "completed",
                ApprovalState = approvalState,
                WorkingDirectory = runtimeProfile.ProjectRoot,
                Output = JsonSerializer.Serialize(activeSession, JsonOptions),
                ChangedFiles = []
            };
        }

        var candidatePath = File.Exists(statusPath) ? statusPath : resultPath;
        if (!File.Exists(candidatePath))
        {
            return Error($"Arena session '{sessionId}' was not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var content = await File.ReadAllTextAsync(candidatePath, cancellationToken);
        return new NativeToolExecutionResult
        {
            ToolName = "arena",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Output = content,
            ChangedFiles = []
        };
    }

    private NativeToolExecutionResult CleanupArenaSession(
        WorkspacePaths paths,
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        var sessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error("Parameter 'session_id' is required for arena cleanup.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sanitizedSessionId = SanitizePathSegment(sessionId);
        gitWorktreeService.CleanupManagedSession(paths, new CleanupManagedWorktreeSessionRequest
        {
            SessionId = sanitizedSessionId
        });

        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "arena", sanitizedSessionId);
        if (Directory.Exists(sessionDirectory))
        {
            Directory.Delete(sessionDirectory, recursive: true);
        }

        arenaSessionRegistry.Remove(sanitizedSessionId, $"Arena session '{sanitizedSessionId}' cleaned up.");

        return new NativeToolExecutionResult
        {
            ToolName = "arena",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Output = $"Arena session '{sanitizedSessionId}' cleaned up.",
            ChangedFiles = []
        };
    }

    private async Task<NativeToolExecutionResult> DiscardArenaSessionAsync(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState,
        CancellationToken cancellationToken)
    {
        var sessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error("Parameter 'session_id' is required for arena discard.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sanitizedSessionId = SanitizePathSegment(sessionId);
        if (arenaSessionRegistry.ListActiveSessions().Any(item => string.Equals(item.SessionId, sanitizedSessionId, StringComparison.OrdinalIgnoreCase)))
        {
            return Error($"Arena session '{sanitizedSessionId}' is still active. Cancel it before discarding.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sessionDirectory = Path.Combine(runtimeProfile.GlobalQwenDirectory, "arena", sanitizedSessionId);
        var configPath = Path.Combine(sessionDirectory, "config.json");
        if (!File.Exists(configPath))
        {
            return Error($"Arena session '{sanitizedSessionId}' was not found.", runtimeProfile.ProjectRoot, approvalState);
        }

        var config = JsonSerializer.Deserialize<ArenaSessionConfigFile>(
            await File.ReadAllTextAsync(configPath, cancellationToken));
        if (config is null)
        {
            return Error($"Arena session '{sanitizedSessionId}' has an invalid config.json.", runtimeProfile.ProjectRoot, approvalState);
        }

        RemoveManagedArenaWorktrees(config.SourceRepoPath, sanitizedSessionId);

        var statusPath = Path.Combine(sessionDirectory, "status.json");
        var resultPath = Path.Combine(sessionDirectory, "result.json");
        var status = File.Exists(statusPath)
            ? JsonSerializer.Deserialize<ArenaSessionStatusFile>(await File.ReadAllTextAsync(statusPath, cancellationToken))
            : null;
        var result = File.Exists(resultPath)
            ? JsonSerializer.Deserialize<ArenaSessionResult>(await File.ReadAllTextAsync(resultPath, cancellationToken))
            : null;

        var updatedConfig = new ArenaSessionConfigFile
        {
            ArenaSessionId = config.ArenaSessionId,
            SourceRepoPath = config.SourceRepoPath,
            Task = config.Task,
            TaskId = config.TaskId,
            RoundCount = config.RoundCount,
            SelectedWinner = config.SelectedWinner,
            AppliedWinner = config.AppliedWinner,
            Models = config.Models,
            WorktreeNames = config.WorktreeNames,
            BaseBranch = config.BaseBranch,
            CreatedAtUtc = config.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            Agents = config.Agents
        };

        var updatedStatus = new ArenaSessionStatusFile
        {
            SessionId = updatedConfig.ArenaSessionId,
            Task = updatedConfig.Task,
            Status = "discarded",
            BaseBranch = updatedConfig.BaseBranch,
            RoundCount = updatedConfig.RoundCount,
            SelectedWinner = updatedConfig.SelectedWinner,
            AppliedWinner = updatedConfig.AppliedWinner,
            StartedAtUtc = status?.StartedAtUtc ?? updatedConfig.CreatedAtUtc,
            EndedAtUtc = DateTime.UtcNow,
            Stats = status?.Stats ?? BuildArenaSessionStats(updatedConfig.Agents.Values.ToArray(), updatedConfig.CreatedAtUtc, DateTime.UtcNow, updatedConfig.RoundCount),
            Agents = status?.Agents ?? updatedConfig.Agents.Values.ToArray()
        };

        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(updatedConfig, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(updatedStatus, JsonOptions), cancellationToken);

        if (result is not null)
        {
            var updatedResult = new ArenaSessionResult
            {
                SessionId = result.SessionId,
                Task = result.Task,
                TaskId = result.TaskId,
                Status = "discarded",
                BaseBranch = result.BaseBranch,
                ArtifactPath = result.ArtifactPath,
                RoundCount = result.RoundCount,
                SelectedWinner = result.SelectedWinner,
                AppliedWinner = result.AppliedWinner,
                CleanupRequested = result.CleanupRequested,
                StartedAtUtc = result.StartedAtUtc,
                EndedAtUtc = DateTime.UtcNow,
                Stats = CloneSessionStats(result.Stats),
                Models = result.Models,
                Agents = result.Agents
            };
            await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(updatedResult, JsonOptions), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(config.TaskId))
        {
            await TryUpdateLinkedTaskAsync(runtimeProfile, config.TaskId, "cancelled", $"arena:{sanitizedSessionId}", cancellationToken);
        }

        return new NativeToolExecutionResult
        {
            ToolName = "arena",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = config.SourceRepoPath,
            Output = $"Discarded arena session '{sanitizedSessionId}' and removed managed worktrees while preserving arena artifacts.",
            ChangedFiles = new[] { configPath, statusPath, resultPath }
                .Where(File.Exists)
                .ToArray()
        };
    }

    private NativeToolExecutionResult CancelArenaSession(
        QwenRuntimeProfile runtimeProfile,
        JsonElement arguments,
        string approvalState)
    {
        var sessionId = TryGetOptionalString(arguments, "session_id") ?? TryGetOptionalString(arguments, "sessionId");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error("Parameter 'session_id' is required for arena cancellation.", runtimeProfile.ProjectRoot, approvalState);
        }

        var sanitizedSessionId = SanitizePathSegment(sessionId);
        if (!arenaSessionRegistry.Cancel(sanitizedSessionId, $"Cancellation requested for arena session '{sanitizedSessionId}'.")) 
        {
            return Error($"Arena session '{sanitizedSessionId}' is not active.", runtimeProfile.ProjectRoot, approvalState);
        }

        return new NativeToolExecutionResult
        {
            ToolName = "arena",
            Status = "completed",
            ApprovalState = approvalState,
            WorkingDirectory = runtimeProfile.ProjectRoot,
            Output = $"Cancellation requested for arena session '{sanitizedSessionId}'.",
            ChangedFiles = []
        };
    }

    private IAssistantTurnRuntime ResolveRuntime()
    {
        if (serviceProvider.GetService(typeof(IAssistantTurnRuntime)) is IAssistantTurnRuntime runtime)
        {
            return runtime;
        }

        throw new InvalidOperationException("Assistant runtime is not available for arena execution.");
    }

    private static AssistantRuntimeEvent CloneArenaEvent(string agentName, AssistantRuntimeEvent runtimeEvent) =>
        new()
        {
            Stage = runtimeEvent.Stage,
            Message = runtimeEvent.Message,
            ProviderName = runtimeEvent.ProviderName,
            ToolName = runtimeEvent.ToolName,
            Status = runtimeEvent.Status,
            ContentDelta = runtimeEvent.ContentDelta,
            ContentSnapshot = runtimeEvent.ContentSnapshot,
            AgentName = string.IsNullOrWhiteSpace(runtimeEvent.AgentName) ? agentName : runtimeEvent.AgentName
        };

    private void RemoveManagedArenaWorktrees(string sourceRepoPath, string sessionId)
    {
        var inspection = gitWorktreeService.Inspect(new WorkspacePaths
        {
            WorkspaceRoot = sourceRepoPath
        });

        var managedWorktrees = inspection.Worktrees
            .Where(item => item.IsManaged && string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var worktree in managedWorktrees)
        {
            var removeResult = gitCliService.Run(sourceRepoPath, "worktree", "remove", "--force", worktree.Path);
            if (!removeResult.Success && Directory.Exists(worktree.Path))
            {
                Directory.Delete(worktree.Path, recursive: true);
            }

            if (!string.IsNullOrWhiteSpace(worktree.Branch))
            {
                _ = gitCliService.Run(sourceRepoPath, "branch", "-D", worktree.Branch);
            }
        }

        _ = gitCliService.Run(sourceRepoPath, "worktree", "prune");
    }

    private ArenaSessionResult BuildCancelledArenaSessionResult(
        string sessionId,
        string task,
        string taskId,
        string baseBranch,
        int roundCount,
        string selectedWinner,
        bool cleanup,
        string sessionDirectory,
        string transcriptsDirectory,
        IReadOnlyList<CreatedArenaWorktree> created,
        IReadOnlyList<ArenaAgentStatusFile> agentStatuses,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        IReadOnlyList<ArenaModelDescriptor> models)
    {
        var worktreeByAgent = created.ToDictionary(static item => item.Model.AgentName, StringComparer.OrdinalIgnoreCase);
        var agentResults = agentStatuses
            .Select(status =>
            {
                var hasWorktree = worktreeByAgent.TryGetValue(status.AgentName, out var createdWorktree);
                var worktreePath = hasWorktree ? createdWorktree!.Worktree.Path : status.WorktreePath;
                return new ArenaAgentResult
                {
                    AgentId = status.AgentId,
                    AgentName = status.AgentName,
                    Status = string.IsNullOrWhiteSpace(status.Status) ? "cancelled" : status.Status,
                    ProviderName = status.ProviderName,
                    Model = status.Model,
                    StopReason = string.IsNullOrWhiteSpace(status.StopReason) ? "cancelled" : status.StopReason,
                    Stats = ResolveStatusStats(status, startedAtUtc, endedAtUtc),
                    WorktreePath = worktreePath,
                    Branch = status.Branch,
                    TranscriptPath = Path.Combine(transcriptsDirectory, $"{SanitizePathSegment(status.AgentName)}.jsonl"),
                    Summary = status.FinalSummary,
                    ErrorMessage = string.IsNullOrWhiteSpace(status.Error) ? "Arena session was cancelled." : status.Error,
                    Diff = hasWorktree ? ReadDiff(worktreePath) : string.Empty,
                    ModifiedFiles = hasWorktree ? ReadModifiedFiles(worktreePath) : [],
                    ToolExecutions = [],
                    StartedAtUtc = startedAtUtc,
                    EndedAtUtc = endedAtUtc
                };
            })
            .ToArray();

        return new ArenaSessionResult
        {
            SessionId = sessionId,
            Task = task,
            TaskId = taskId,
            Status = "cancelled",
            BaseBranch = baseBranch,
            ArtifactPath = Path.Combine(sessionDirectory, "result.json"),
            RoundCount = roundCount,
            SelectedWinner = selectedWinner,
            CleanupRequested = cleanup,
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            Stats = BuildArenaSessionStats(agentStatuses, startedAtUtc, endedAtUtc, roundCount),
            Models = models,
            Agents = agentResults
        };
    }

    private void ApplyArenaRuntimeEventToAgentState(
        string sessionId,
        string agentName,
        AssistantRuntimeEvent runtimeEvent)
    {
        if (!ShouldTrackArenaRuntimeEvent(runtimeEvent))
        {
            return;
        }

        UpdateArenaAgentState(
            sessionId,
            agentName,
            ArenaSessionEventKind.AgentStatsUpdated,
            $"Arena agent '{agentName}' updated runtime stats.",
            existing =>
            {
                var stats = CloneStats(existing.Stats);
                if (string.Equals(runtimeEvent.Stage, "generating", StringComparison.OrdinalIgnoreCase))
                {
                    stats = new AssistantExecutionStats
                    {
                        RoundCount = stats.RoundCount + 1,
                        ToolCallCount = stats.ToolCallCount,
                        SuccessfulToolCallCount = stats.SuccessfulToolCallCount,
                        FailedToolCallCount = stats.FailedToolCallCount,
                        DurationMs = Math.Max(stats.DurationMs, (long)Math.Max(0, (DateTime.UtcNow - existing.UpdatedAtUtc).TotalMilliseconds))
                    };
                }
                else if (IsTerminalArenaToolEvent(runtimeEvent))
                {
                    var isSuccess = string.Equals(runtimeEvent.Stage, "tool-completed", StringComparison.OrdinalIgnoreCase);
                    stats = new AssistantExecutionStats
                    {
                        RoundCount = stats.RoundCount,
                        ToolCallCount = stats.ToolCallCount + 1,
                        SuccessfulToolCallCount = stats.SuccessfulToolCallCount + (isSuccess ? 1 : 0),
                        FailedToolCallCount = stats.FailedToolCallCount + (isSuccess ? 0 : 1),
                        DurationMs = stats.DurationMs
                    };
                }

                return new ArenaAgentStatusFile
                {
                    AgentId = existing.AgentId,
                    AgentName = existing.AgentName,
                    Status = existing.Status,
                    Model = existing.Model,
                    StopReason = existing.StopReason,
                    Stats = stats,
                    WorktreeName = existing.WorktreeName,
                    WorktreePath = existing.WorktreePath,
                    Branch = existing.Branch,
                    ProviderName = string.IsNullOrWhiteSpace(runtimeEvent.ProviderName) ? existing.ProviderName : runtimeEvent.ProviderName,
                    CurrentActivity = runtimeEvent.Message,
                    FinalSummary = existing.FinalSummary,
                    Error = existing.Error,
                    UpdatedAtUtc = DateTime.UtcNow
                };
            });
    }

    private void UpdateArenaAgentState(
        string sessionId,
        string agentName,
        ArenaSessionEventKind kind,
        string message,
        Func<ArenaAgentStatusFile, ArenaAgentStatusFile> updateAgent)
    {
        arenaSessionRegistry.Update(
            sessionId,
            state =>
            {
                var updatedAgents = state.Agents
                    .Select(item => string.Equals(item.AgentName, agentName, StringComparison.OrdinalIgnoreCase)
                        ? updateAgent(CloneAgentStatusFile(item))
                        : CloneAgentStatusFile(item))
                    .ToArray();
                state.Agents = updatedAgents;
                state.Status = updatedAgents.Any(static item => string.Equals(item.Status, "error", StringComparison.OrdinalIgnoreCase))
                    ? "error"
                    : "running";
                state.Stats = BuildArenaSessionStats(updatedAgents, state.StartedAtUtc, DateTime.UtcNow, state.RoundCount);
            },
            kind,
            message,
            agentName);
    }

    private static async Task PersistTranscriptAsync(
        string transcriptPath,
        string agentId,
        string workingDirectory,
        string prompt,
        AssistantTurnResponse response,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(transcriptPath)!);
        var lines = new[]
        {
            JsonSerializer.Serialize(new
            {
                id = $"{agentId}:user",
                role = "user",
                content = prompt,
                cwd = workingDirectory,
                timestampUtc = DateTime.UtcNow
            }),
            JsonSerializer.Serialize(new
            {
                id = $"{agentId}:assistant",
                role = "assistant",
                content = response.Summary,
                provider = response.ProviderName,
                model = response.Model,
                timestampUtc = DateTime.UtcNow
            })
        };
        await File.WriteAllTextAsync(
            transcriptPath,
            string.Join(Environment.NewLine, lines) + Environment.NewLine,
            cancellationToken);
    }

    private IReadOnlyList<string> ReadModifiedFiles(string workingDirectory)
    {
        var result = gitCliService.Run(workingDirectory, "status", "--short");
        if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return [];
        }

        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Length <= 3 ? string.Empty : line[3..].Trim())
            .Select(static path => path.Contains(" -> ", StringComparison.Ordinal) ? path.Split(" -> ", StringSplitOptions.TrimEntries)[^1] : path)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
    }

    private string ReadDiff(string workingDirectory)
    {
        var result = gitCliService.Run(workingDirectory, "diff", "--stat", "--patch", "--no-ext-diff");
        return result.Success ? result.StandardOutput.Trim() : string.Empty;
    }

    private static List<CreatedArenaWorktree> BuildCreatedArenaWorktrees(
        ArenaSessionConfigFile config,
        GitRepositorySnapshot inspection)
    {
        var created = new List<CreatedArenaWorktree>();
        foreach (var model in config.Models)
        {
            if (!config.Agents.TryGetValue(model.AgentName, out var agentStatus))
            {
                continue;
            }

            var worktree = inspection.Worktrees.FirstOrDefault(item =>
                item.IsManaged &&
                string.Equals(item.SessionId, config.ArenaSessionId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Name, agentStatus.WorktreeName, StringComparison.OrdinalIgnoreCase));
            if (worktree is null)
            {
                continue;
            }

            created.Add(new CreatedArenaWorktree(model, worktree));
        }

        return created;
    }

    private static async Task WriteInitialSessionFilesAsync(
        string sessionDirectory,
        string sourceRepoPath,
        string task,
        string taskId,
        string sessionId,
        string baseBranch,
        DateTime createdAtUtc,
        IReadOnlyList<CreatedArenaWorktree> created,
        IReadOnlyList<ArenaModelDescriptor> models,
        int roundCount,
        string selectedWinner,
        SemaphoreSlim statusLock,
        CancellationToken cancellationToken)
    {
        var config = new ArenaSessionConfigFile
        {
            ArenaSessionId = sessionId,
            SourceRepoPath = sourceRepoPath,
            Task = task,
            TaskId = taskId,
            RoundCount = roundCount,
            SelectedWinner = selectedWinner,
            AppliedWinner = string.Empty,
            Models = models,
            WorktreeNames = created.Select(static item => item.Worktree.Name).ToArray(),
            BaseBranch = baseBranch,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
            Agents = created.ToDictionary(
                static item => item.Model.AgentName,
                static item => BuildAgentStatusFile(
                    $"{item.Model.AgentName}",
                    item,
                    "initializing",
                    "Arena competitor prepared.",
                    string.Empty,
                    string.Empty,
                    DateTime.UtcNow),
                StringComparer.OrdinalIgnoreCase)
        };

        var status = new ArenaSessionStatusFile
        {
            SessionId = sessionId,
            Task = task,
            Status = "initializing",
            BaseBranch = baseBranch,
            RoundCount = roundCount,
            SelectedWinner = selectedWinner,
            AppliedWinner = string.Empty,
            StartedAtUtc = createdAtUtc,
            Stats = BuildArenaSessionStats(config.Agents.Values.ToArray(), createdAtUtc, createdAtUtc, roundCount),
            Agents = config.Agents.Values.ToArray()
        };

        await WriteSessionFilesAsync(sessionDirectory, config, status, statusLock, cancellationToken);
    }

    private static async Task WriteFinalSessionFilesAsync(
        string sessionDirectory,
        string sourceRepoPath,
        string task,
        string taskId,
        ArenaSessionResult sessionResult,
        SemaphoreSlim statusLock,
        CancellationToken cancellationToken)
    {
        var config = new ArenaSessionConfigFile
        {
            ArenaSessionId = sessionResult.SessionId,
            SourceRepoPath = sourceRepoPath,
            Task = task,
            TaskId = string.IsNullOrWhiteSpace(taskId) ? sessionResult.TaskId : taskId,
            RoundCount = sessionResult.RoundCount,
            SelectedWinner = sessionResult.SelectedWinner,
            AppliedWinner = sessionResult.AppliedWinner,
            Models = sessionResult.Models,
            WorktreeNames = sessionResult.Agents.Select(static item => Path.GetFileName(item.WorktreePath)).ToArray(),
            BaseBranch = sessionResult.BaseBranch,
            CreatedAtUtc = sessionResult.StartedAtUtc,
            UpdatedAtUtc = sessionResult.EndedAtUtc,
            Agents = sessionResult.Agents.ToDictionary(
                static item => item.AgentName,
                static item => new ArenaAgentStatusFile
                {
                    AgentId = item.AgentId,
                    AgentName = item.AgentName,
                    Status = item.Status,
                    Model = item.Model,
                    StopReason = item.StopReason,
                    Stats = CloneStats(item.Stats),
                    WorktreeName = Path.GetFileName(item.WorktreePath),
                    WorktreePath = item.WorktreePath,
                    Branch = item.Branch,
                    ProviderName = item.ProviderName,
                    FinalSummary = item.Summary,
                    Error = item.ErrorMessage,
                    UpdatedAtUtc = item.EndedAtUtc
                },
                StringComparer.OrdinalIgnoreCase)
        };

        var status = new ArenaSessionStatusFile
        {
            SessionId = sessionResult.SessionId,
            Task = sessionResult.Task,
            Status = sessionResult.Status,
            BaseBranch = sessionResult.BaseBranch,
            RoundCount = sessionResult.RoundCount,
            SelectedWinner = sessionResult.SelectedWinner,
            AppliedWinner = sessionResult.AppliedWinner,
            StartedAtUtc = sessionResult.StartedAtUtc,
            EndedAtUtc = sessionResult.EndedAtUtc,
            Stats = CloneSessionStats(sessionResult.Stats),
            Agents = config.Agents.Values.ToArray()
        };

        await WriteSessionFilesAsync(sessionDirectory, config, status, statusLock, cancellationToken);
    }

    private static async Task WriteAgentStatusAsync(
        string sessionDirectory,
        string agentStatusesDirectory,
        string sourceRepoPath,
        string task,
        DateTime sessionStartedAtUtc,
        ArenaAgentStatusFile agentStatus,
        SemaphoreSlim statusLock,
        CancellationToken cancellationToken)
    {
        await statusLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(agentStatusesDirectory);
            var safeName = SanitizePathSegment(agentStatus.AgentName);
            var agentStatusPath = Path.Combine(agentStatusesDirectory, $"{safeName}.json");
            await File.WriteAllTextAsync(agentStatusPath, JsonSerializer.Serialize(agentStatus, JsonOptions), cancellationToken);

            var configPath = Path.Combine(sessionDirectory, "config.json");
            ArenaSessionConfigFile? existingConfig = null;
            if (File.Exists(configPath))
            {
                existingConfig = JsonSerializer.Deserialize<ArenaSessionConfigFile>(
                    await File.ReadAllTextAsync(configPath, cancellationToken));
            }

            if (existingConfig is null)
            {
                existingConfig = new ArenaSessionConfigFile
                {
                    ArenaSessionId = Path.GetFileName(sessionDirectory),
                    SourceRepoPath = sourceRepoPath,
                    Task = task,
                    TaskId = string.Empty,
                    RoundCount = 1,
                    CreatedAtUtc = sessionStartedAtUtc
                };
            }

            var agents = new Dictionary<string, ArenaAgentStatusFile>(existingConfig.Agents, StringComparer.OrdinalIgnoreCase)
            {
                [agentStatus.AgentName] = agentStatus
            };
            var updatedConfig = new ArenaSessionConfigFile
            {
                ArenaSessionId = existingConfig.ArenaSessionId,
                SourceRepoPath = existingConfig.SourceRepoPath,
                Task = existingConfig.Task,
                TaskId = existingConfig.TaskId,
                RoundCount = existingConfig.RoundCount,
                SelectedWinner = existingConfig.SelectedWinner,
                AppliedWinner = existingConfig.AppliedWinner,
                Models = existingConfig.Models,
                WorktreeNames = existingConfig.WorktreeNames,
                BaseBranch = existingConfig.BaseBranch,
                CreatedAtUtc = existingConfig.CreatedAtUtc,
                UpdatedAtUtc = agentStatus.UpdatedAtUtc,
                Agents = agents
            };

            var sessionStatus = agents.Values.All(static item => string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase))
                ? "completed"
                : agents.Values.Any(static item => string.Equals(item.Status, "error", StringComparison.OrdinalIgnoreCase))
                    ? "error"
                    : "running";
            var status = new ArenaSessionStatusFile
            {
                SessionId = updatedConfig.ArenaSessionId,
                Task = updatedConfig.Task,
                Status = sessionStatus,
                BaseBranch = updatedConfig.BaseBranch,
                RoundCount = updatedConfig.RoundCount,
                SelectedWinner = updatedConfig.SelectedWinner,
                AppliedWinner = updatedConfig.AppliedWinner,
                StartedAtUtc = updatedConfig.CreatedAtUtc,
                EndedAtUtc = sessionStatus is "completed" or "error" ? agentStatus.UpdatedAtUtc : null,
                Stats = BuildArenaSessionStats(
                    agents.Values.OrderBy(static item => item.AgentName, StringComparer.OrdinalIgnoreCase).ToArray(),
                    updatedConfig.CreatedAtUtc,
                    agentStatus.UpdatedAtUtc,
                    updatedConfig.RoundCount),
                Agents = agents.Values.OrderBy(static item => item.AgentName, StringComparer.OrdinalIgnoreCase).ToArray()
            };

            await WriteSessionFilesUnsafeAsync(sessionDirectory, updatedConfig, status, cancellationToken);
        }
        finally
        {
            statusLock.Release();
        }
    }

    private static async Task WriteSessionFilesAsync(
        string sessionDirectory,
        ArenaSessionConfigFile config,
        ArenaSessionStatusFile status,
        SemaphoreSlim statusLock,
        CancellationToken cancellationToken)
    {
        await statusLock.WaitAsync(cancellationToken);
        try
        {
            await WriteSessionFilesUnsafeAsync(sessionDirectory, config, status, cancellationToken);
        }
        finally
        {
            statusLock.Release();
        }
    }

    private static async Task WriteSessionFilesUnsafeAsync(
        string sessionDirectory,
        ArenaSessionConfigFile config,
        ArenaSessionStatusFile status,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(sessionDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(sessionDirectory, "config.json"),
            JsonSerializer.Serialize(config, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(sessionDirectory, "status.json"),
            JsonSerializer.Serialize(status, JsonOptions),
            cancellationToken);
    }

    private static ArenaAgentStatusFile BuildAgentStatusFile(
        string agentId,
        CreatedArenaWorktree created,
        string status,
        string currentActivity,
        string finalSummary,
        string error,
        DateTime updatedAtUtc,
        string providerName = "",
        string stopReason = "",
        AssistantExecutionStats? stats = null)
    {
        return new ArenaAgentStatusFile
        {
            AgentId = agentId,
            AgentName = created.Model.AgentName,
            Status = status,
            Model = created.Model.Model,
            StopReason = stopReason,
            Stats = CloneStats(stats),
            WorktreeName = created.Worktree.Name,
            WorktreePath = created.Worktree.Path,
            Branch = created.Worktree.Branch,
            ProviderName = providerName,
            CurrentActivity = currentActivity,
            FinalSummary = finalSummary,
            Error = error,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    private static ArenaAgentStatusFile CloneAgentStatusFile(ArenaAgentStatusFile source) =>
        new()
        {
            AgentId = source.AgentId,
            AgentName = source.AgentName,
            Status = source.Status,
            Model = source.Model,
            StopReason = source.StopReason,
            Stats = CloneStats(source.Stats),
            WorktreeName = source.WorktreeName,
            WorktreePath = source.WorktreePath,
            Branch = source.Branch,
            ProviderName = source.ProviderName,
            CurrentActivity = source.CurrentActivity,
            FinalSummary = source.FinalSummary,
            Error = source.Error,
            UpdatedAtUtc = source.UpdatedAtUtc
        };

    private static AssistantExecutionStats CloneStats(AssistantExecutionStats? stats) =>
        stats is null
            ? new AssistantExecutionStats()
            : new AssistantExecutionStats
            {
                RoundCount = stats.RoundCount,
                ToolCallCount = stats.ToolCallCount,
                SuccessfulToolCallCount = stats.SuccessfulToolCallCount,
                FailedToolCallCount = stats.FailedToolCallCount,
                DurationMs = stats.DurationMs
            };

    private static AssistantExecutionStats ResolveStatusStats(
        ArenaAgentStatusFile status,
        DateTime startedAtUtc,
        DateTime endedAtUtc)
    {
        if (status.Stats.RoundCount > 0 || status.Stats.ToolCallCount > 0 || status.Stats.DurationMs > 0)
        {
            return CloneStats(status.Stats);
        }

        return AssistantExecutionDiagnostics.BuildStats(
            1,
            [],
            Math.Max(0L, (long)(endedAtUtc - startedAtUtc).TotalMilliseconds));
    }

    private static ArenaSessionStats BuildArenaSessionStats(
        IReadOnlyList<ArenaAgentStatusFile> agents,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        int roundCount)
    {
        var completedAgentCount = agents.Count(static item =>
            string.Equals(item.Status, "completed", StringComparison.OrdinalIgnoreCase));
        var failedAgentCount = agents.Count(static item =>
            string.Equals(item.Status, "error", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, "blocked", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Status, "cancelled", StringComparison.OrdinalIgnoreCase));
        return new ArenaSessionStats
        {
            AgentCount = agents.Count,
            CompletedAgentCount = completedAgentCount,
            FailedAgentCount = failedAgentCount,
            RoundCount = Math.Max(roundCount, agents.Count == 0 ? 0 : agents.Max(static item => item.Stats.RoundCount)),
            ToolCallCount = agents.Sum(static item => item.Stats.ToolCallCount),
            SuccessfulToolCallCount = agents.Sum(static item => item.Stats.SuccessfulToolCallCount),
            FailedToolCallCount = agents.Sum(static item => item.Stats.FailedToolCallCount),
            TotalDurationMs = Math.Max(0L, (long)(endedAtUtc - startedAtUtc).TotalMilliseconds)
        };
    }

    private static ArenaSessionStats CloneSessionStats(ArenaSessionStats stats) =>
        new()
        {
            AgentCount = stats.AgentCount,
            CompletedAgentCount = stats.CompletedAgentCount,
            FailedAgentCount = stats.FailedAgentCount,
            RoundCount = stats.RoundCount,
            ToolCallCount = stats.ToolCallCount,
            SuccessfulToolCallCount = stats.SuccessfulToolCallCount,
            FailedToolCallCount = stats.FailedToolCallCount,
            TotalDurationMs = stats.TotalDurationMs
        };

    private static ArenaAgentStatusFile MapArenaAgentResultToStatusFile(ArenaAgentResult item) =>
        new()
        {
            AgentId = item.AgentId,
            AgentName = item.AgentName,
            Status = item.Status,
            Model = item.Model,
            StopReason = item.StopReason,
            Stats = CloneStats(item.Stats),
            WorktreeName = Path.GetFileName(item.WorktreePath),
            WorktreePath = item.WorktreePath,
            Branch = item.Branch,
            ProviderName = item.ProviderName,
            FinalSummary = item.Summary,
            Error = item.ErrorMessage,
            UpdatedAtUtc = item.EndedAtUtc
        };

    private static bool ShouldTrackArenaRuntimeEvent(AssistantRuntimeEvent runtimeEvent) =>
        string.Equals(runtimeEvent.Stage, "generating", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(runtimeEvent.Stage, "assembling-context", StringComparison.OrdinalIgnoreCase) ||
        IsTerminalArenaToolEvent(runtimeEvent);

    private static bool IsTerminalArenaToolEvent(AssistantRuntimeEvent runtimeEvent) =>
        string.Equals(runtimeEvent.Stage, "tool-completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(runtimeEvent.Stage, "tool-failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(runtimeEvent.Stage, "tool-blocked", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(runtimeEvent.Stage, "tool-approval-required", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(runtimeEvent.Stage, "user-input-required", StringComparison.OrdinalIgnoreCase);

    private static string ResolveArenaAgentStatus(AssistantTurnResponse response)
    {
        var toolStatus = response.ToolExecutions.LastOrDefault()?.Execution.Status;
        return string.IsNullOrWhiteSpace(toolStatus) || string.Equals(toolStatus, "completed", StringComparison.OrdinalIgnoreCase)
            ? "completed"
            : toolStatus;
    }

    private static string BuildArenaReport(ArenaSessionResult session)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Arena session '{session.SessionId}' finished with status '{session.Status}'.");
        builder.AppendLine($"Round: {session.RoundCount}");
        builder.AppendLine($"Task: {session.Task}");
        if (!string.IsNullOrWhiteSpace(session.TaskId))
        {
            builder.AppendLine($"Linked task: #{session.TaskId}");
        }
        builder.AppendLine($"Base branch: {session.BaseBranch}");
        builder.AppendLine($"Session stats: agents={session.Stats.AgentCount}, completed={session.Stats.CompletedAgentCount}, failed={session.Stats.FailedAgentCount}, rounds={session.Stats.RoundCount}, toolCalls={session.Stats.ToolCallCount}, successful={session.Stats.SuccessfulToolCallCount}, failedTools={session.Stats.FailedToolCallCount}, durationMs={session.Stats.TotalDurationMs}");
        if (!string.IsNullOrWhiteSpace(session.SelectedWinner))
        {
            builder.AppendLine($"Selected winner: {session.SelectedWinner}");
        }
        if (!string.IsNullOrWhiteSpace(session.AppliedWinner))
        {
            builder.AppendLine($"Applied winner: {session.AppliedWinner}");
        }
        builder.AppendLine();

        foreach (var agent in session.Agents.OrderBy(static item => item.AgentName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"[{agent.AgentName}] {agent.Status} ({agent.Model})");
            builder.AppendLine($"Worktree: {agent.WorktreePath}");
            builder.AppendLine($"Stop reason: {agent.StopReason}");
            builder.AppendLine($"Stats: rounds={agent.Stats.RoundCount}, toolCalls={agent.Stats.ToolCallCount}, successful={agent.Stats.SuccessfulToolCallCount}, failed={agent.Stats.FailedToolCallCount}, durationMs={agent.Stats.DurationMs}");
            if (!string.IsNullOrWhiteSpace(agent.Summary))
            {
                builder.AppendLine($"Summary: {agent.Summary}");
            }

            if (agent.ModifiedFiles.Count > 0)
            {
                builder.AppendLine($"Modified files: {string.Join(", ", agent.ModifiedFiles)}");
            }

            if (!string.IsNullOrWhiteSpace(agent.ErrorMessage))
            {
                builder.AppendLine($"Error: {agent.ErrorMessage}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildArenaModeSpecificInstructions(ArenaModelDescriptor model, IReadOnlyList<string> allowedToolNames)
    {
        var allowedTools = allowedToolNames.Count == 0
            ? "inherit the native desktop tool surface"
            : string.Join(", ", allowedToolNames);

        return
        $$"""
Competitor label: {{model.AgentName}}
Requested model: {{model.Model}}

Arena rules:
- Work only inside your assigned worktree. Do not assume access to sibling arena worktrees or the source repository checkout.
- Use only the tools allowed for this arena run: {{allowedTools}}.
- Produce the strongest end-to-end attempt you can within your worktree instead of narrating strategy.
- Verify meaningful changes when feasible and call out anything you could not verify.
- Do not mention the competition or compare yourself to other agents in the final answer.
""";
    }

    private static string BuildArenaTaskPrompt(string task, ArenaModelDescriptor model) =>
        $$"""
Arena task:
{{task}}

You are competing as "{{model.AgentName}}" using model "{{model.Model}}".
Success criteria:
- Solve as much of the task as you can from inside your worktree.
- Prefer concrete changes and verification over high-level discussion.
- Leave a concise final summary with changed files, validation performed, tradeoffs, and residual risks.
""";

    private static List<ArenaModelDescriptor> ParseModels(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var models = new List<ArenaModelDescriptor>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in modelsElement.EnumerateArray())
        {
            index++;
            if (item.ValueKind == JsonValueKind.String)
            {
                var modelName = item.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    continue;
                }

                var agentName = MakeUniqueName(SanitizePathSegment(modelName), usedNames);
                models.Add(new ArenaModelDescriptor
                {
                    AgentName = agentName,
                    DisplayName = modelName,
                    Model = modelName
                });
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var model = TryReadString(item, "model") ?? TryReadString(item, "model_id");
            if (string.IsNullOrWhiteSpace(model))
            {
                continue;
            }

            var displayName = TryReadString(item, "display_name") ?? model;
            var requestedName = TryReadString(item, "agent_name") ?? displayName ?? $"agent-{index}";
            var resolvedAgentName = MakeUniqueName(SanitizePathSegment(requestedName), usedNames);
            models.Add(new ArenaModelDescriptor
            {
                AgentName = resolvedAgentName,
                DisplayName = displayName ?? resolvedAgentName,
                Model = model,
                AuthType = TryReadString(item, "auth_type") ?? string.Empty,
                ApiKey = TryReadString(item, "api_key") ?? string.Empty,
                BaseUrl = TryReadString(item, "base_url") ?? string.Empty
            });
        }

        return models;
    }

    private static string MakeUniqueName(string baseName, ISet<string> usedNames)
    {
        var seed = string.IsNullOrWhiteSpace(baseName) ? "agent" : baseName;
        var candidate = seed;
        var suffix = 2;
        while (!usedNames.Add(candidate))
        {
            candidate = $"{seed}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string BuildWorktreeName(ArenaModelDescriptor model, int index)
    {
        var seed = string.IsNullOrWhiteSpace(model.AgentName)
            ? $"agent-{index + 1}"
            : model.AgentName;
        return seed;
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? TryGetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool? TryGetBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : null;

    private static string? TryReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static async Task TryUpdateLinkedTaskAsync(
        QwenRuntimeProfile runtimeProfile,
        string taskId,
        string status,
        string owner,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        using var document = JsonDocument.Parse(
            new JsonObject
            {
                ["task_id"] = taskId,
                ["status"] = status,
                ["owner"] = owner
            }.ToJsonString());
        await TaskStore.UpdateTaskAsync(runtimeProfile, document.RootElement, cancellationToken);
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

        return string.IsNullOrWhiteSpace(sanitized) ? "agent" : sanitized;
    }

    private static NativeToolExecutionResult Error(string message, string workingDirectory, string approvalState) =>
        new()
        {
            ToolName = "arena",
            Status = "error",
            ApprovalState = approvalState,
            WorkingDirectory = workingDirectory,
            ErrorMessage = message,
            ChangedFiles = []
        };

    private sealed record CreatedArenaWorktree(ArenaModelDescriptor Model, GitWorktreeEntry Worktree);
}
