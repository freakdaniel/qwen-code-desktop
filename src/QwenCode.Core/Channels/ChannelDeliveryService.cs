using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;
using QwenCode.Core.Sessions;

namespace QwenCode.Core.Channels;

/// <summary>
/// Represents the Channel Delivery Service
/// </summary>
public sealed class ChannelDeliveryService : IChannelDeliveryService
{
    private readonly IChannelRegistryService channelRegistry;
    private readonly IChannelSessionRouter sessionRouter;
    private readonly IDesktopEnvironmentPaths environmentPaths;
    private readonly IReadOnlyDictionary<string, IChannelAdapter> adapterMap;
    private readonly Lock streamGate = new();
    private readonly Dictionary<string, ActiveChannelStream> activeStreams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the ChannelDeliveryService class
    /// </summary>
    /// <param name="sessionHost">The session host</param>
    /// <param name="channelRegistry">The channel registry</param>
    /// <param name="sessionRouter">The session router</param>
    /// <param name="adapters">The adapters</param>
    /// <param name="environmentPaths">The environment paths</param>
    public ChannelDeliveryService(
        ISessionHost sessionHost,
        IChannelRegistryService channelRegistry,
        IChannelSessionRouter sessionRouter,
        IEnumerable<IChannelAdapter> adapters,
        IDesktopEnvironmentPaths environmentPaths)
    {
        this.channelRegistry = channelRegistry;
        this.sessionRouter = sessionRouter;
        this.environmentPaths = environmentPaths;
        adapterMap = adapters.ToDictionary(static item => item.ChannelType, StringComparer.OrdinalIgnoreCase);

        sessionHost.SessionEvent += OnSessionEvent;
    }

    /// <summary>
    /// Executes deliver async
    /// </summary>
    /// <param name="sessionEvent">The session event</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DeliverAsync(DesktopSessionEvent sessionEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var route = sessionRouter.ListRoutes()
            .FirstOrDefault(item => string.Equals(item.SessionId, sessionEvent.SessionId, StringComparison.OrdinalIgnoreCase));
        if (route is null)
        {
            return;
        }

        var outbound = MapMessage(route, sessionEvent);
        if (outbound is null)
        {
            CleanupStream(sessionEvent);
            return;
        }

        IChannelAdapter? adapter = null;
        ChannelRuntimeConfiguration? runtimeConfiguration = null;
        try
        {
            runtimeConfiguration = channelRegistry.GetRuntimeConfiguration(
                new WorkspacePaths { WorkspaceRoot = route.WorkingDirectory },
                route.ChannelName);
            var definition = channelRegistry.GetChannel(
                new WorkspacePaths { WorkspaceRoot = route.WorkingDirectory },
                route.ChannelName);
            adapterMap.TryGetValue(definition.Type, out adapter);
        }
        catch
        {
        }

        if (adapter is null)
        {
            Persist(outbound);
            return;
        }

        if (runtimeConfiguration is not null &&
            await TryHandleStreamedDeliveryAsync(runtimeConfiguration, adapter, route, sessionEvent, cancellationToken))
        {
            return;
        }

        var payload = adapter.CreateOutboundPayload(route, outbound);
        await DeliverViaAdapterAsync(adapter, runtimeConfiguration, route, outbound, payload, cancellationToken);
    }

    /// <summary>
    /// Replays queued async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to int</returns>
    public async Task<int> ReplayQueuedAsync(WorkspacePaths workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outboxRoot = GetOutboxRoot();
        if (!Directory.Exists(outboxRoot))
        {
            return 0;
        }

        var replayedCount = 0;
        foreach (var path in Directory.GetFiles(outboxRoot, "*.jsonl", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
            {
                continue;
            }

            var updatedLines = new List<string>(lines.Length);
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                JsonObject? record;
                try
                {
                    record = JsonNode.Parse(line) as JsonObject;
                }
                catch
                {
                    updatedLines.Add(line);
                    continue;
                }

                if (record is null)
                {
                    updatedLines.Add(line);
                    continue;
                }

                if (!string.Equals(GetString(record, "deliveryStatus"), "queued", StringComparison.OrdinalIgnoreCase))
                {
                    updatedLines.Add(record.ToJsonString());
                    continue;
                }

                var delivered = await TryReplayQueuedRecordAsync(workspace, record, cancellationToken);
                if (delivered)
                {
                    replayedCount++;
                }

                updatedLines.Add(record.ToJsonString());
            }

            File.WriteAllLines(path, updatedLines);
        }

        return replayedCount;
    }

    private void OnSessionEvent(object? sender, DesktopSessionEvent sessionEvent)
    {
        _ = DeliverAsync(sessionEvent);
    }

    private void Persist(ChannelOutboundMessage message, JsonNode? payload = null)
    {
        Persist(message, payload, "queued");
    }

    private void Persist(ChannelOutboundMessage message, JsonNode? payload, string deliveryStatus)
    {
        var outboxRoot = GetOutboxRoot();
        Directory.CreateDirectory(outboxRoot);
        var path = Path.Combine(outboxRoot, $"{message.ChannelName}.jsonl");

        var record = new
        {
            channelName = message.ChannelName,
            sessionId = message.SessionId,
            chatId = message.ChatId,
            senderId = message.SenderId,
            kind = message.Kind,
            text = message.Text,
            toolName = message.ToolName,
            commandName = message.CommandName,
            timestampUtc = message.TimestampUtc,
            deliveryStatus,
            payload = payload
        };

        File.AppendAllText(path, JsonSerializer.Serialize(record) + Environment.NewLine);
    }

    private async Task DeliverViaAdapterAsync(
        IChannelAdapter adapter,
        ChannelRuntimeConfiguration? runtimeConfiguration,
        ChannelSessionRoute route,
        ChannelOutboundMessage outbound,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        var delivered = false;
        if (runtimeConfiguration is not null)
        {
            try
            {
                delivered = await adapter.SendOutboundAsync(runtimeConfiguration, route, outbound, payload, cancellationToken);
            }
            catch
            {
                delivered = false;
            }
        }

        Persist(outbound, payload, delivered ? "delivered" : "queued");
    }

    private async Task<bool> TryHandleStreamedDeliveryAsync(
        ChannelRuntimeConfiguration configuration,
        IChannelAdapter adapter,
        ChannelSessionRoute route,
        DesktopSessionEvent sessionEvent,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(configuration.BlockStreaming, "on", StringComparison.OrdinalIgnoreCase))
        {
            CleanupStream(sessionEvent);
            return false;
        }

        switch (sessionEvent.Kind)
        {
            case DesktopSessionEventKind.AssistantStreaming:
            {
                if (string.IsNullOrWhiteSpace(sessionEvent.ContentDelta))
                {
                    return true;
                }

                var stream = GetOrCreateStream(configuration, adapter, route);
                stream.Streamer.Push(sessionEvent.ContentDelta);
                return true;
            }

            case DesktopSessionEventKind.AssistantCompleted:
            {
                var stream = GetStream(sessionEvent.SessionId);
                if (stream is null)
                {
                    return false;
                }

                var flushedAny = await stream.Streamer.FlushAsync();
                await stream.Streamer.DisposeAsync();
                RemoveStream(sessionEvent.SessionId);
                return flushedAny;
            }

            case DesktopSessionEventKind.TurnCancelled:
            case DesktopSessionEventKind.TurnInterrupted:
            case DesktopSessionEventKind.TurnCompleted:
                await DisposeAndRemoveStreamAsync(sessionEvent.SessionId);
                return false;

            default:
                return false;
        }
    }

    private static ChannelOutboundMessage? MapMessage(ChannelSessionRoute route, DesktopSessionEvent sessionEvent)
    {
        return sessionEvent.Kind switch
        {
            DesktopSessionEventKind.AssistantStreaming when !string.IsNullOrWhiteSpace(sessionEvent.ContentDelta) => Create(
                route,
                "chunk",
                sessionEvent.ContentDelta,
                sessionEvent),
            DesktopSessionEventKind.AssistantCompleted => Create(route, "message", sessionEvent.Message, sessionEvent),
            DesktopSessionEventKind.ToolApprovalRequired => Create(route, "approval-required", sessionEvent.Message, sessionEvent),
            DesktopSessionEventKind.UserInputRequired => Create(route, "input-required", sessionEvent.Message, sessionEvent),
            DesktopSessionEventKind.ToolFailed => Create(route, "tool-failed", sessionEvent.Message, sessionEvent),
            DesktopSessionEventKind.ToolBlocked => Create(route, "tool-blocked", sessionEvent.Message, sessionEvent),
            DesktopSessionEventKind.TurnCancelled => Create(route, "turn-cancelled", sessionEvent.Message, sessionEvent),
            DesktopSessionEventKind.TurnCompleted => Create(route, "turn-completed", sessionEvent.Message, sessionEvent),
            _ => null
        };
    }

    private static ChannelOutboundMessage Create(
        ChannelSessionRoute route,
        string kind,
        string text,
        DesktopSessionEvent sessionEvent) =>
        new()
        {
            ChannelName = route.ChannelName,
            SessionId = route.SessionId,
            ChatId = route.ChatId,
            SenderId = route.SenderId,
            Kind = kind,
            Text = text,
            ToolName = sessionEvent.ToolName,
            CommandName = sessionEvent.CommandName,
            TimestampUtc = sessionEvent.TimestampUtc
        };

    private ActiveChannelStream GetOrCreateStream(
        ChannelRuntimeConfiguration configuration,
        IChannelAdapter adapter,
        ChannelSessionRoute route)
    {
        lock (streamGate)
        {
            if (activeStreams.TryGetValue(route.SessionId, out var existing))
            {
                return existing;
            }

            var streamer = new ChannelBlockStreamer(
                block => SendStreamBlockAsync(configuration, adapter, route, block),
                configuration.BlockStreamingChunk.MinChars,
                configuration.BlockStreamingChunk.MaxChars,
                configuration.BlockStreamingCoalesce.IdleMs);
            var created = new ActiveChannelStream(route.SessionId, streamer);
            activeStreams[route.SessionId] = created;
            return created;
        }
    }

    private ActiveChannelStream? GetStream(string sessionId)
    {
        lock (streamGate)
        {
            return activeStreams.TryGetValue(sessionId, out var stream) ? stream : null;
        }
    }

    private void RemoveStream(string sessionId)
    {
        lock (streamGate)
        {
            activeStreams.Remove(sessionId);
        }
    }

    private void CleanupStream(DesktopSessionEvent sessionEvent)
    {
        switch (sessionEvent.Kind)
        {
            case DesktopSessionEventKind.TurnCancelled:
            case DesktopSessionEventKind.TurnInterrupted:
            case DesktopSessionEventKind.TurnCompleted:
                _ = DisposeAndRemoveStreamAsync(sessionEvent.SessionId);
                break;
        }
    }

    private async Task DisposeAndRemoveStreamAsync(string sessionId)
    {
        ActiveChannelStream? stream;
        lock (streamGate)
        {
            stream = activeStreams.TryGetValue(sessionId, out var existing) ? existing : null;
            activeStreams.Remove(sessionId);
        }

        if (stream is not null)
        {
            await stream.Streamer.DisposeAsync();
        }
    }

    private async Task SendStreamBlockAsync(
        ChannelRuntimeConfiguration configuration,
        IChannelAdapter adapter,
        ChannelSessionRoute route,
        string block)
    {
        var outbound = new ChannelOutboundMessage
        {
            ChannelName = route.ChannelName,
            SessionId = route.SessionId,
            ChatId = route.ChatId,
            SenderId = route.SenderId,
            Kind = "message-block",
            Text = block,
            TimestampUtc = DateTime.UtcNow
        };

        var payload = adapter.CreateOutboundPayload(route, outbound);
        var delivered = false;
        try
        {
            delivered = await adapter.SendOutboundAsync(configuration, route, outbound, payload);
        }
        catch
        {
            delivered = false;
        }

        Persist(outbound, payload, delivered ? "delivered" : "queued");
    }

    private sealed record ActiveChannelStream(string SessionId, ChannelBlockStreamer Streamer);

    private async Task<bool> TryReplayQueuedRecordAsync(
        WorkspacePaths workspace,
        JsonObject record,
        CancellationToken cancellationToken)
    {
        var channelName = GetString(record, "channelName");
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return false;
        }

        ChannelRuntimeConfiguration configuration;
        IChannelAdapter adapter;
        try
        {
            configuration = channelRegistry.GetRuntimeConfiguration(workspace, channelName);
            var definition = channelRegistry.GetChannel(workspace, channelName);
            if (!adapterMap.TryGetValue(definition.Type, out adapter!))
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        var route = ResolveReplayRoute(record, configuration);
        if (route is null)
        {
            return false;
        }

        var outbound = new ChannelOutboundMessage
        {
            ChannelName = channelName,
            SessionId = GetString(record, "sessionId"),
            ChatId = GetString(record, "chatId"),
            SenderId = GetString(record, "senderId"),
            Kind = GetString(record, "kind"),
            Text = GetString(record, "text"),
            ToolName = GetString(record, "toolName"),
            CommandName = GetString(record, "commandName"),
            TimestampUtc = GetDateTime(record, "timestampUtc")
        };

        var payload = record["payload"] as JsonObject ?? adapter.CreateOutboundPayload(route, outbound);
        var delivered = false;
        try
        {
            delivered = await adapter.SendOutboundAsync(configuration, route, outbound, payload, cancellationToken);
        }
        catch
        {
            delivered = false;
        }

        record["payload"] = payload.DeepClone();
        record["deliveryStatus"] = delivered ? "delivered" : "queued";
        return delivered;
    }

    private ChannelSessionRoute? ResolveReplayRoute(JsonObject record, ChannelRuntimeConfiguration configuration)
    {
        var sessionId = GetString(record, "sessionId");
        var channelName = GetString(record, "channelName");
        var route = sessionRouter.ListRoutes().FirstOrDefault(item =>
            string.Equals(item.SessionId, sessionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.ChannelName, channelName, StringComparison.OrdinalIgnoreCase));
        if (route is not null)
        {
            return route;
        }

        var payload = record["payload"] as JsonObject;
        var chatId = GetString(record, "chatId");
        if (string.IsNullOrWhiteSpace(chatId))
        {
            chatId = GetString(payload, "chatId");
        }

        var senderId = GetString(record, "senderId");
        if (string.IsNullOrWhiteSpace(senderId))
        {
            senderId = GetString(payload, "senderId");
        }

        if (string.IsNullOrWhiteSpace(channelName) ||
            string.IsNullOrWhiteSpace(sessionId) ||
            string.IsNullOrWhiteSpace(chatId))
        {
            return null;
        }

        return new ChannelSessionRoute
        {
            SessionId = sessionId,
            ChannelName = channelName,
            SenderId = senderId,
            ChatId = chatId,
            ThreadId = GetString(payload, "threadId"),
            ReplyAddress = GetString(payload, "replyAddress"),
            WorkingDirectory = string.IsNullOrWhiteSpace(GetString(payload, "workingDirectory"))
                ? configuration.WorkingDirectory
                : GetString(payload, "workingDirectory")
        };
    }

    private string GetOutboxRoot() => Path.Combine(environmentPaths.HomeDirectory, ".qwen", "channels", "outbox");

    private static string GetString(JsonObject? node, string propertyName)
    {
        if (node is null || !node.TryGetPropertyValue(propertyName, out var value) || value is null)
        {
            return string.Empty;
        }

        return value.GetValue<string?>() ?? string.Empty;
    }

    private static DateTime GetDateTime(JsonObject node, string propertyName)
    {
        var raw = GetString(node, propertyName);
        return DateTime.TryParse(raw, out var value) ? value : DateTime.UtcNow;
    }
}
