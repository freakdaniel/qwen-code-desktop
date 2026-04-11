using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;
using QwenCode.Core.Sessions;

namespace QwenCode.Core.Channels;

/// <summary>
/// Represents the Channel Runtime Service
/// </summary>
/// <param name="channelRegistry">The channel registry</param>
/// <param name="channelPluginRuntime">The channel plugin runtime</param>
/// <param name="channelAdapters">The channel adapters</param>
/// <param name="sessionRouter">The session router</param>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="channelDelivery">The channel delivery</param>
/// <param name="sessionHost">The session host</param>
public sealed class ChannelRuntimeService(
    IChannelRegistryService channelRegistry,
    IChannelPluginRuntimeService channelPluginRuntime,
    IEnumerable<IChannelAdapter> channelAdapters,
    IChannelSessionRouter sessionRouter,
    IDesktopEnvironmentPaths environmentPaths,
    IChannelDeliveryService channelDelivery,
    ISessionHost sessionHost) : IChannelRuntimeService
{
    private static readonly TimeSpan OutboxDrainInterval = TimeSpan.FromMilliseconds(250);
    private readonly IReadOnlyDictionary<string, IChannelAdapter> adapters = channelAdapters.ToDictionary(
        static item => item.ChannelType,
        StringComparer.OrdinalIgnoreCase);
    private readonly Lock dispatchGate = new();
    private readonly Dictionary<string, Task> sessionQueues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<BufferedChannelMessage>> collectBuffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock lifecycleGate = new();
    private CancellationTokenSource? runtimeLoopCts;
    private Task? runtimeLoopTask;
    private WorkspacePaths? activeWorkspace;

    /// <summary>
    /// Gets snapshot
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <returns>The resulting channel snapshot</returns>
    public ChannelSnapshot GetSnapshot(WorkspacePaths workspace) => channelRegistry.Inspect(workspace);

    /// <summary>
    /// Starts async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel snapshot</returns>
    public async Task<ChannelSnapshot> StartAsync(WorkspacePaths workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = channelRegistry.Inspect(workspace);
        var channelsRoot = GetChannelsRoot();
        Directory.CreateDirectory(channelsRoot);
        var info = new
        {
            Pid = Process.GetCurrentProcess().Id,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            Channels = snapshot.Channels.Select(static item => item.Name).ToArray()
        };
        File.WriteAllText(
            Path.Combine(channelsRoot, "service.pid"),
            JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        _ = sessionRouter.ListRoutes();

        await channelPluginRuntime.StartAsync(workspace, snapshot.Channels, cancellationToken);
        await ConnectConfiguredAdaptersAsync(workspace, snapshot, cancellationToken);
        StartBackgroundDrainLoop(workspace);
        return await ReplayAndReturnSnapshotAsync(workspace, cancellationToken);
    }

    private async Task<ChannelSnapshot> ReplayAndReturnSnapshotAsync(
        WorkspacePaths workspace,
        CancellationToken cancellationToken)
    {
        _ = await channelDelivery.ReplayQueuedAsync(workspace, cancellationToken);
        return channelRegistry.Inspect(workspace);
    }

    /// <summary>
    /// Stops async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await StopBackgroundDrainLoopAsync();
        await channelPluginRuntime.StopAsync(cancellationToken);
        await DisconnectConfiguredAdaptersAsync(cancellationToken);
        var path = Path.Combine(GetChannelsRoot(), "service.pid");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        sessionRouter.Clear();
    }

    private async Task ConnectConfiguredAdaptersAsync(
        WorkspacePaths workspace,
        ChannelSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        foreach (var channel in snapshot.Channels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (channelPluginRuntime.IsPluginChannel(workspace, channel.Type))
            {
                continue;
            }

            if (!adapters.TryGetValue(channel.Type, out var adapter))
            {
                continue;
            }

            try
            {
                var configuration = channelRegistry.GetRuntimeConfiguration(workspace, channel.Name);
                await adapter.ConnectAsync(configuration, cancellationToken);
            }
            catch
            {
            }
        }
    }

    private async Task DisconnectConfiguredAdaptersAsync(CancellationToken cancellationToken)
    {
        WorkspacePaths? workspace;
        lock (lifecycleGate)
        {
            workspace = activeWorkspace;
            activeWorkspace = null;
        }

        if (workspace is null)
        {
            return;
        }

        var snapshot = channelRegistry.Inspect(workspace);
        foreach (var channel in snapshot.Channels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (channelPluginRuntime.IsPluginChannel(workspace, channel.Type))
            {
                continue;
            }

            if (!adapters.TryGetValue(channel.Type, out var adapter))
            {
                continue;
            }

            try
            {
                var configuration = channelRegistry.GetRuntimeConfiguration(workspace, channel.Name);
                await adapter.DisconnectAsync(configuration, cancellationToken);
            }
            catch
            {
            }
        }
    }

    private void StartBackgroundDrainLoop(WorkspacePaths workspace)
    {
        lock (lifecycleGate)
        {
            runtimeLoopCts?.Cancel();
            runtimeLoopCts?.Dispose();
            runtimeLoopCts = new CancellationTokenSource();
            activeWorkspace = workspace;
            runtimeLoopTask = RunBackgroundDrainLoopAsync(workspace, runtimeLoopCts.Token);
        }
    }

    private async Task StopBackgroundDrainLoopAsync()
    {
        Task? loopTask;
        CancellationTokenSource? cts;
        lock (lifecycleGate)
        {
            loopTask = runtimeLoopTask;
            cts = runtimeLoopCts;
            runtimeLoopTask = null;
            runtimeLoopCts = null;
        }

        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        try
        {
            if (loopTask is not null)
            {
                await loopTask;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task RunBackgroundDrainLoopAsync(WorkspacePaths workspace, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(OutboxDrainInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                _ = await channelDelivery.ReplayQueuedAsync(workspace, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// Executes handle inbound async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelName">The channel name</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel dispatch result</returns>
    public async Task<ChannelDispatchResult> HandleInboundAsync(
        WorkspacePaths workspace,
        string channelName,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var channel = channelRegistry.GetChannel(workspace, channelName);
        var configuration = channelRegistry.GetRuntimeConfiguration(workspace, channelName);
        if (channelPluginRuntime.IsPluginChannel(workspace, channel.Type))
        {
            return await channelPluginRuntime.HandleInboundAsync(workspace, channel, configuration, payload, cancellationToken);
        }

        if (!adapters.TryGetValue(channel.Type, out var adapter))
        {
            throw new InvalidOperationException($"Channel type '{channel.Type}' is not supported by the native runtime.");
        }

        var envelope = adapter.NormalizeInbound(channelName, payload);
        var groupDecision = EvaluateGroupAccess(configuration, envelope);
        if (!groupDecision.Allowed)
        {
            return new ChannelDispatchResult
            {
                ChannelName = channelName,
                Status = "blocked",
                Message = groupDecision.Reason
            };
        }

        var senderAccess = channelRegistry.EvaluateSenderAccess(workspace, channelName, envelope.SenderId, envelope.SenderName);
        if (!senderAccess.Allowed)
        {
            return new ChannelDispatchResult
            {
                ChannelName = channelName,
                Status = string.IsNullOrWhiteSpace(senderAccess.PairingCode) ? "blocked" : "pairing-required",
                PairingCode = senderAccess.PairingCode,
                Message = senderAccess.Reason
            };
        }

        var localCommandResult = HandleLocalCommand(channel, configuration, envelope);
        if (localCommandResult is not null)
        {
            return localCommandResult;
        }

        var route = await sessionRouter.ResolveAsync(
            channelName,
            configuration.SessionScope,
            envelope.SenderId,
            envelope.ChatId,
            envelope.ThreadId,
            envelope.ReplyAddress,
            configuration.WorkingDirectory,
            cancellationToken);

        var prompt = BuildPrompt(configuration, envelope);
        var dispatchMode = ResolveDispatchMode(configuration, envelope);
        if (string.Equals(dispatchMode, "collect", StringComparison.OrdinalIgnoreCase) &&
            IsSessionQueued(route.SessionId))
        {
            BufferCollectMessage(route.SessionId, prompt);
            return new ChannelDispatchResult
            {
                ChannelName = channelName,
                Status = "buffered",
                SessionId = route.SessionId,
                Message = "Message buffered for the active channel session."
            };
        }

        if (string.Equals(dispatchMode, "steer", StringComparison.OrdinalIgnoreCase) &&
            IsSessionQueued(route.SessionId))
        {
            _ = await sessionHost.CancelTurnAsync(
                workspace,
                new CancelDesktopSessionTurnRequest { SessionId = route.SessionId },
                cancellationToken);
            prompt =
                "[The user sent a new channel message while you were still working. Cancel the previous request and continue from this latest instruction.]" +
                Environment.NewLine +
                Environment.NewLine +
                prompt;
        }

        ChannelDispatchResult? dispatchResult = null;
        await EnqueueAsync(route.SessionId, async () =>
        {
            dispatchResult = await DispatchTurnAsync(workspace, channelName, route, prompt, cancellationToken);

            if (string.Equals(dispatchMode, "collect", StringComparison.OrdinalIgnoreCase))
            {
                await DrainCollectBufferAsync(workspace, channelName, route, cancellationToken);
            }
        });

        return dispatchResult ?? new ChannelDispatchResult
        {
            ChannelName = channelName,
            Status = "error",
            SessionId = route.SessionId,
            Message = "Channel dispatch did not produce a result."
        };
    }

    private async Task<ChannelDispatchResult> DispatchTurnAsync(
        WorkspacePaths workspace,
        string channelName,
        ChannelSessionRoute route,
        string prompt,
        CancellationToken cancellationToken)
    {
        var result = await sessionHost.StartTurnAsync(
            workspace,
            new StartDesktopSessionTurnRequest
            {
                SessionId = route.SessionId,
                Prompt = prompt,
                WorkingDirectory = route.WorkingDirectory
            },
            cancellationToken);

        return new ChannelDispatchResult
        {
            ChannelName = channelName,
            Status = "dispatched",
            SessionId = result.Session.SessionId,
            Message = result.AssistantSummary,
            TranscriptPath = result.Session.TranscriptPath,
            CreatedNewSession = result.CreatedNewSession,
            AssistantSummary = result.AssistantSummary
        };
    }

    private async Task DrainCollectBufferAsync(
        WorkspacePaths workspace,
        string channelName,
        ChannelSessionRoute route,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string? bufferedPrompt;
            lock (dispatchGate)
            {
                if (!collectBuffers.TryGetValue(route.SessionId, out var buffer) || buffer.Count == 0)
                {
                    collectBuffers.Remove(route.SessionId);
                    return;
                }

                bufferedPrompt = string.Join(
                    Environment.NewLine + Environment.NewLine,
                    buffer.Select(static item => item.Prompt));
                buffer.Clear();
            }

            _ = await DispatchTurnAsync(workspace, channelName, route, bufferedPrompt, cancellationToken);
        }
    }

    private ChannelDispatchResult? HandleLocalCommand(
        ChannelDefinition channel,
        ChannelRuntimeConfiguration configuration,
        ChannelEnvelope envelope)
    {
        var command = ParseSlashCommand(envelope.Text);
        if (command is null)
        {
            return null;
        }

        return command.Name switch
        {
            "help" => new ChannelDispatchResult
            {
                ChannelName = channel.Name,
                Status = "local-command",
                Message = BuildHelpMessage(channel, configuration)
            },
            "status" => new ChannelDispatchResult
            {
                ChannelName = channel.Name,
                Status = "local-command",
                Message = BuildStatusMessage(channel, envelope)
            },
            "clear" or "reset" or "new" => new ChannelDispatchResult
            {
                ChannelName = channel.Name,
                Status = "local-command",
                Message = BuildClearMessage(envelope)
            },
            _ => null
        };
    }

    private string BuildStatusMessage(ChannelDefinition channel, ChannelEnvelope envelope)
    {
        var hasSession = sessionRouter.HasSession(channel.Name, envelope.SenderId, envelope.ChatId);
        return string.Join(
            Environment.NewLine,
            [
                $"Session: {(hasSession ? "active" : "none")}",
                $"Access: {channel.SenderPolicy}",
                $"Channel: {channel.Name}"
            ]);
    }

    private string BuildClearMessage(ChannelEnvelope envelope)
    {
        var removed = sessionRouter.RemoveSessions(envelope.ChannelName, envelope.SenderId, envelope.ChatId);
        return removed.Count > 0
            ? "Session cleared. Your next message will start a fresh conversation."
            : "No active session to clear.";
    }

    private static string BuildHelpMessage(ChannelDefinition channel, ChannelRuntimeConfiguration configuration)
    {
        var lines = new List<string>
        {
            "Commands:",
            "/help - Show this help",
            "/clear - Clear your session (aliases: /reset, /new)",
            "/status - Show channel session info",
            string.Empty,
            $"Channel: {channel.Name}",
            $"Dispatch mode: {configuration.DispatchMode}",
            $"Session scope: {configuration.SessionScope}"
        };

        if (!string.IsNullOrWhiteSpace(configuration.Instructions))
        {
            lines.Add("Channel instructions are configured for the first message of a session.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static SlashCommand? ParseSlashCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('/'))
        {
            return null;
        }

        var commandText = text[1..].Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return null;
        }

        var commandName = commandText.Split([' ', '@'], 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(commandName)
            ? null
            : new SlashCommand(commandName.ToLowerInvariant());
    }

    private static GroupAccessDecision EvaluateGroupAccess(ChannelRuntimeConfiguration configuration, ChannelEnvelope envelope)
    {
        if (!envelope.IsGroup)
        {
            return GroupAccessDecision.Allow();
        }

        if (string.Equals(configuration.GroupPolicy, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return GroupAccessDecision.Deny($"Group messages are disabled for channel '{configuration.Name}'.");
        }

        var groupOverride = configuration.Groups.FirstOrDefault(item =>
            string.Equals(item.ChatId, envelope.ChatId, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(configuration.GroupPolicy, "allowlist", StringComparison.OrdinalIgnoreCase) && groupOverride is null)
        {
            return GroupAccessDecision.Deny($"Group '{envelope.ChatId}' is not allowlisted for channel '{configuration.Name}'.");
        }

        var requireMention = groupOverride?.RequireMention ?? configuration.RequireMentionByDefault;
        if (requireMention && !envelope.IsMentioned && !envelope.IsReplyToBot)
        {
            return GroupAccessDecision.Deny("A mention or reply to the bot is required for group messages.");
        }

        return GroupAccessDecision.Allow();
    }

    private static string ResolveDispatchMode(ChannelRuntimeConfiguration configuration, ChannelEnvelope envelope)
    {
        if (envelope.IsGroup)
        {
            var groupMode = configuration.Groups
                .FirstOrDefault(item => string.Equals(item.ChatId, envelope.ChatId, StringComparison.OrdinalIgnoreCase))
                ?.DispatchMode;
            if (!string.IsNullOrWhiteSpace(groupMode))
            {
                return groupMode;
            }
        }

        return string.IsNullOrWhiteSpace(configuration.DispatchMode) ? "collect" : configuration.DispatchMode;
    }

    private static string BuildPrompt(ChannelRuntimeConfiguration configuration, ChannelEnvelope envelope)
    {
        var prompt = string.IsNullOrWhiteSpace(envelope.ReferencedText)
            ? envelope.Text
            : $"{envelope.Text}{Environment.NewLine}{Environment.NewLine}Referenced message:{Environment.NewLine}{envelope.ReferencedText}";

        var attachmentSummary = BuildAttachmentSummary(envelope);
        if (!string.IsNullOrWhiteSpace(attachmentSummary))
        {
            prompt += Environment.NewLine + Environment.NewLine + attachmentSummary;
        }

        if (!string.IsNullOrWhiteSpace(configuration.Instructions))
        {
            prompt = configuration.Instructions + Environment.NewLine + Environment.NewLine + prompt;
        }

        return prompt;
    }

    private static string BuildAttachmentSummary(ChannelEnvelope envelope)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(envelope.ImageBase64))
        {
            lines.Add($"Embedded image attached ({(string.IsNullOrWhiteSpace(envelope.ImageMimeType) ? "unknown mime" : envelope.ImageMimeType)}).");
        }

        if (envelope.Attachments.Count > 0)
        {
            lines.Add("Attachments:");
            lines.AddRange(envelope.Attachments.Select(static item =>
            {
                var name = string.IsNullOrWhiteSpace(item.FileName) ? "<unnamed>" : item.FileName;
                var type = string.IsNullOrWhiteSpace(item.Type) ? "file" : item.Type;
                var source = !string.IsNullOrWhiteSpace(item.FilePath)
                    ? item.FilePath
                    : !string.IsNullOrWhiteSpace(item.Data)
                        ? "inline-data"
                        : "unknown-source";
                return $"- {type}: {name} ({source})";
            }));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private bool IsSessionQueued(string sessionId)
    {
        lock (dispatchGate)
        {
            return sessionQueues.TryGetValue(sessionId, out var existing) && !existing.IsCompleted;
        }
    }

    private void BufferCollectMessage(string sessionId, string prompt)
    {
        lock (dispatchGate)
        {
            if (!collectBuffers.TryGetValue(sessionId, out var buffer))
            {
                buffer = [];
                collectBuffers[sessionId] = buffer;
            }

            buffer.Add(new BufferedChannelMessage(prompt));
        }
    }

    private async Task EnqueueAsync(string sessionId, Func<Task> action)
    {
        Task previous;
        lock (dispatchGate)
        {
            previous = sessionQueues.TryGetValue(sessionId, out var existing) ? existing : Task.CompletedTask;
        }

        var current = previous.ContinueWith(
            async _ => await action(),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default).Unwrap();

        lock (dispatchGate)
        {
            sessionQueues[sessionId] = current;
        }

        try
        {
            await current;
        }
        finally
        {
            lock (dispatchGate)
            {
                if (sessionQueues.TryGetValue(sessionId, out var tracked) && ReferenceEquals(tracked, current))
                {
                    sessionQueues.Remove(sessionId);
                }
            }
        }
    }

    private string GetChannelsRoot() => Path.Combine(environmentPaths.HomeDirectory, ".qwen", "channels");

    private sealed record SlashCommand(string Name);

    private sealed record BufferedChannelMessage(string Prompt);

    private sealed record GroupAccessDecision(bool Allowed, string Reason)
    {
        /// <summary>
        /// Executes allow
        /// </summary>
        /// <returns>The resulting group access decision</returns>
        public static GroupAccessDecision Allow() => new(true, string.Empty);

        /// <summary>
        /// Executes deny
        /// </summary>
        /// <param name="reason">The reason</param>
        /// <returns>The resulting group access decision</returns>
        public static GroupAccessDecision Deny(string reason) => new(false, reason);
    }
}
