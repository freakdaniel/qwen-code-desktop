using QwenCode.Core.Models;
using QwenCode.Core.Sessions;

namespace QwenCode.Core.Channels;

/// <summary>
/// Represents the Channel Plugin Runtime Service
/// </summary>
/// <param name="pluginRegistry">The plugin registry</param>
/// <param name="channelRegistry">The channel registry</param>
/// <param name="sessionHost">The session host</param>
public sealed class ChannelPluginRuntimeService(
    IChannelPluginRegistryService pluginRegistry,
    IChannelRegistryService channelRegistry,
    ISessionHost sessionHost) : IChannelPluginRuntimeService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Lock sync = new();
    private readonly Dictionary<string, PluginHostSession> hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PluginHostSession> sessionOwners = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Executes is plugin channel
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelType">The channel type</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool IsPluginChannel(WorkspacePaths workspace, string channelType) =>
        pluginRegistry.GetPlugin(workspace, channelType) is not null;

    /// <summary>
    /// Starts async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channels">The channels</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task StartAsync(
        WorkspacePaths workspace,
        IReadOnlyList<ChannelDefinition> channels,
        CancellationToken cancellationToken = default)
    {
        var pluginChannels = channels
            .Where(channel => pluginRegistry.GetPlugin(workspace, channel.Type) is not null)
            .ToArray();

        foreach (var channel in pluginChannels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureHostAsync(workspace, channel.Name, cancellationToken);
        }
    }

    /// <summary>
    /// Stops async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        List<PluginHostSession> activeHosts;
        lock (sync)
        {
            activeHosts = hosts.Values.ToList();
            hosts.Clear();
        }

        foreach (var host in activeHosts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await host.DisposeAsync(cancellationToken);
        }

        sessionOwners.Clear();
    }

    /// <summary>
    /// Executes handle inbound async
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channel">The channel</param>
    /// <param name="configuration">The configuration to apply</param>
    /// <param name="payload">The payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel dispatch result</returns>
    public async Task<ChannelDispatchResult> HandleInboundAsync(
        WorkspacePaths workspace,
        ChannelDefinition channel,
        ChannelRuntimeConfiguration configuration,
        JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var host = await EnsureHostAsync(workspace, channel.Name, cancellationToken);
        var envelope = JsonSerializer.Deserialize<PluginInboundEnvelope>(
            payload.GetRawText(),
            JsonOptions) ?? throw new InvalidOperationException("Invalid plugin channel envelope payload.");
        var resolvedEnvelope = new ChannelEnvelope
        {
            ChannelName = channel.Name,
            SenderId = envelope.SenderId,
            SenderName = envelope.SenderName,
            ChatId = envelope.ChatId,
            Text = envelope.Text,
            ThreadId = envelope.ThreadId,
            ReplyAddress = envelope.ReplyAddress,
            IsGroup = envelope.IsGroup,
            IsMentioned = envelope.IsMentioned,
            IsReplyToBot = envelope.IsReplyToBot,
            ReferencedText = envelope.ReferencedText,
            ImageBase64 = envelope.ImageBase64,
            ImageMimeType = envelope.ImageMimeType,
            Attachments = envelope.Attachments
        };
        var result = await host.HandleEnvelopeAsync(resolvedEnvelope, cancellationToken);
        return new ChannelDispatchResult
        {
            ChannelName = channel.Name,
            Status = "dispatched",
            Message = result,
            AssistantSummary = result,
            SessionId = string.Empty
        };
    }

    private async Task<PluginHostSession> EnsureHostAsync(
        WorkspacePaths workspace,
        string channelName,
        CancellationToken cancellationToken)
    {
        lock (sync)
        {
            if (hosts.TryGetValue(channelName, out var existing))
            {
                return existing;
            }
        }

        var channel = channelRegistry.GetChannel(workspace, channelName);
        var plugin = pluginRegistry.GetPlugin(workspace, channel.Type)
            ?? throw new InvalidOperationException($"Channel type '{channel.Type}' is not backed by an extension plugin.");
        var configuration = channelRegistry.GetRuntimeConfiguration(workspace, channelName);
        var host = await PluginHostSession.StartAsync(
            workspace,
            channelName,
            plugin,
            configuration,
            GetNodeExecutable(),
            ResolvePluginHostScriptPath(),
            sessionHost,
            sessionOwners,
            cancellationToken);

        lock (sync)
        {
            hosts[channelName] = host;
        }

        return host;
    }

    private static string GetNodeExecutable() =>
        Environment.GetEnvironmentVariable("NODE_BINARY") is { Length: > 0 } configured
            ? configured
            : "node";

    private static string ResolvePluginHostScriptPath()
    {
        var coreAssemblyDirectory = Path.GetDirectoryName(typeof(ChannelPluginRuntimeService).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Channels", "plugin-host", "channel-plugin-host.mjs"),
            Path.Combine(AppContext.BaseDirectory, "channel-plugin-host.mjs"),
            Path.GetFullPath(Path.Combine(coreAssemblyDirectory, "..", "..", "..", "Channels", "plugin-host", "channel-plugin-host.mjs"))
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("Channel plugin host script was not found.", candidates[0]);
    }

    /// <summary>
    /// Executes dispose
    /// </summary>
    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    private sealed class PluginHostSession
    {
        private readonly Lock messageSync = new();
        private readonly SemaphoreSlim writeGate = new(1, 1);
        private readonly Dictionary<string, TaskCompletionSource<JsonObject>> pendingRequests = new(StringComparer.Ordinal);
        private readonly Process process;
        private readonly StreamWriter writer;
        private readonly WorkspacePaths workspace;
        private readonly string channelName;
        private readonly string channelType;
        private readonly ISessionHost sessionHost;
        private readonly ConcurrentDictionary<string, PluginHostSession> sessionOwners;
        private Task stdoutPump = Task.CompletedTask;
        private Task stderrPump = Task.CompletedTask;

        private PluginHostSession(
            Process process,
            StreamWriter writer,
            WorkspacePaths workspace,
            string channelName,
            string channelType,
            ISessionHost sessionHost,
            ConcurrentDictionary<string, PluginHostSession> sessionOwners)
        {
            this.process = process;
            this.writer = writer;
            this.workspace = workspace;
            this.channelName = channelName;
            this.channelType = channelType;
            this.sessionHost = sessionHost;
            this.sessionOwners = sessionOwners;
        }

        /// <summary>
        /// Starts async
        /// </summary>
        /// <param name="workspace">The workspace</param>
        /// <param name="channelName">The channel name</param>
        /// <param name="plugin">The plugin</param>
        /// <param name="configuration">The configuration to apply</param>
        /// <param name="nodeExecutable">The node executable</param>
        /// <param name="hostScriptPath">The host script path</param>
        /// <param name="sessionHost">The session host</param>
        /// <param name="sessionOwners">The session owners</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to plugin host session</returns>
        public static async Task<PluginHostSession> StartAsync(
            WorkspacePaths workspace,
            string channelName,
            ChannelPluginDefinition plugin,
            ChannelRuntimeConfiguration configuration,
            string nodeExecutable,
            string hostScriptPath,
            ISessionHost sessionHost,
            ConcurrentDictionary<string, PluginHostSession> sessionOwners,
            CancellationToken cancellationToken)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = nodeExecutable,
                    ArgumentList = { hostScriptPath },
                    WorkingDirectory = plugin.ExtensionPath,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start channel plugin host for '{channelName}'.");
            }

            var session = new PluginHostSession(
                process,
                process.StandardInput,
                workspace,
                channelName,
                plugin.ChannelType,
                sessionHost,
                sessionOwners);

            process.Exited += (_, _) => session.FailPendingRequests($"Channel plugin host '{channelName}' exited unexpectedly.");
            session.stdoutPump = session.RunStdoutPumpAsync(process.StandardOutput, cancellationToken);
            session.stderrPump = session.RunStderrPumpAsync(process.StandardError, cancellationToken);
            session.sessionHost.SessionEvent += session.OnSessionEvent;

            var readyMessage = await session.SendRequestAsync(
                new JsonObject
                {
                    ["type"] = "init",
                    ["channelName"] = channelName,
                    ["entryPath"] = plugin.EntryPath,
                    ["config"] = SerializeConfiguration(configuration)
                },
                cancellationToken);

            var expectedType = readyMessage["channelType"]?.GetValue<string>() ?? string.Empty;
            if (!string.Equals(expectedType, plugin.ChannelType, StringComparison.OrdinalIgnoreCase))
            {
                await session.DisposeAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Extension plugin '{plugin.ExtensionName}' exported channelType '{expectedType}', expected '{plugin.ChannelType}'.");
            }

            return session;
        }

        /// <summary>
        /// Executes handle envelope async
        /// </summary>
        /// <param name="envelope">The envelope</param>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that resolves to string</returns>
        public async Task<string> HandleEnvelopeAsync(ChannelEnvelope envelope, CancellationToken cancellationToken)
        {
            var reply = await SendRequestAsync(
                new JsonObject
                {
                    ["type"] = "handleEnvelope",
                    ["envelope"] = JsonSerializer.SerializeToNode(envelope, JsonOptions)
                },
                cancellationToken);

            return reply["assistantSummary"]?.GetValue<string>() ?? string.Empty;
        }

        /// <summary>
        /// Executes dispose async
        /// </summary>
        /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task DisposeAsync(CancellationToken cancellationToken)
        {
            sessionHost.SessionEvent -= OnSessionEvent;
            try
            {
                if (!process.HasExited)
                {
                    await SendRequestAsync(new JsonObject { ["type"] = "disconnect" }, cancellationToken);
                }
            }
            catch
            {
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            try
            {
                if (!process.HasExited)
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
            }
            catch
            {
            }

            writer.Dispose();
            writeGate.Dispose();
            process.Dispose();
        }

        private async Task<JsonObject> SendRequestAsync(JsonObject payload, CancellationToken cancellationToken)
        {
            var requestId = Guid.NewGuid().ToString("N");
            payload["requestId"] = requestId;

            var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (messageSync)
            {
                pendingRequests[requestId] = completion;
            }

            try
            {
                await WriteMessageAsync(payload, cancellationToken);
                using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                return await completion.Task;
            }
            finally
            {
                lock (messageSync)
                {
                    pendingRequests.Remove(requestId);
                }
            }
        }

        private async Task RunStdoutPumpAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                JsonObject? message;
                try
                {
                    message = JsonNode.Parse(line) as JsonObject;
                }
                catch
                {
                    continue;
                }

                if (message is null)
                {
                    continue;
                }

                var type = message["type"]?.GetValue<string>() ?? string.Empty;
                switch (type)
                {
                    case "response":
                        CompleteRequest(message);
                        break;
                    case "prompt":
                        _ = HandlePromptAsync(message, cancellationToken);
                        break;
                    case "cancel":
                        _ = HandleCancelAsync(message, cancellationToken);
                        break;
                }
            }
        }

        private async Task RunStderrPumpAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (await reader.ReadLineAsync(cancellationToken) is null)
                {
                    break;
                }
            }
        }

        private void CompleteRequest(JsonObject response)
        {
            var requestId = response["requestId"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return;
            }

            TaskCompletionSource<JsonObject>? completion;
            lock (messageSync)
            {
                pendingRequests.TryGetValue(requestId, out completion);
            }

            if (completion is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(response["error"]?.GetValue<string>()))
            {
                completion.TrySetException(new InvalidOperationException(response["error"]!.GetValue<string>()));
                return;
            }

            completion.TrySetResult(response);
        }

        private async Task HandlePromptAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var requestId = message["requestId"]?.GetValue<string>() ?? string.Empty;
            var sessionId = message["sessionId"]?.GetValue<string>() ?? string.Empty;
            var prompt = message["text"]?.GetValue<string>() ?? string.Empty;
            var workingDirectory = message["cwd"]?.GetValue<string>() ?? workspace.WorkspaceRoot;

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                sessionOwners[sessionId] = this;
            }

            try
            {
                var result = await sessionHost.StartTurnAsync(
                    workspace,
                    new StartDesktopSessionTurnRequest
                    {
                        SessionId = sessionId,
                        Prompt = prompt,
                        WorkingDirectory = workingDirectory
                    },
                    cancellationToken);

                await SendHostMessageAsync(
                    new JsonObject
                    {
                        ["type"] = "promptResult",
                        ["requestId"] = requestId,
                        ["response"] = result.AssistantSummary
                    },
                    cancellationToken);
            }
            catch (Exception exception)
            {
                await SendHostMessageAsync(
                    new JsonObject
                    {
                        ["type"] = "promptError",
                        ["requestId"] = requestId,
                        ["error"] = exception.Message
                    },
                    cancellationToken);
            }
        }

        private async Task HandleCancelAsync(JsonObject message, CancellationToken cancellationToken)
        {
            var sessionId = message["sessionId"]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            _ = await sessionHost.CancelTurnAsync(
                workspace,
                new CancelDesktopSessionTurnRequest { SessionId = sessionId },
                cancellationToken);
        }

        private async void OnSessionEvent(object? sender, DesktopSessionEvent sessionEvent)
        {
            if (!string.Equals(sessionEvent.Kind.ToString(), DesktopSessionEventKind.AssistantStreaming.ToString(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(sessionEvent.ContentDelta))
            {
                return;
            }

            if (!sessionOwners.TryGetValue(sessionEvent.SessionId, out var owner) || !ReferenceEquals(owner, this))
            {
                return;
            }

            try
            {
                await SendHostMessageAsync(
                    new JsonObject
                    {
                        ["type"] = "textChunk",
                        ["sessionId"] = sessionEvent.SessionId,
                        ["chunk"] = sessionEvent.ContentDelta
                    },
                    CancellationToken.None);
            }
            catch
            {
            }
        }

        private async Task SendHostMessageAsync(JsonObject message, CancellationToken cancellationToken)
        {
            await WriteMessageAsync(message, cancellationToken);
        }

        private void FailPendingRequests(string error)
        {
            List<TaskCompletionSource<JsonObject>> completions;
            lock (messageSync)
            {
                completions = pendingRequests.Values.ToList();
                pendingRequests.Clear();
            }

            foreach (var completion in completions)
            {
                completion.TrySetException(new InvalidOperationException(error));
            }
        }

        private async Task WriteMessageAsync(JsonObject message, CancellationToken cancellationToken)
        {
            await writeGate.WaitAsync(cancellationToken);
            try
            {
                await writer.WriteLineAsync(message.ToJsonString(JsonOptions));
                await writer.FlushAsync();
            }
            finally
            {
                writeGate.Release();
            }
        }

        private static JsonObject SerializeConfiguration(ChannelRuntimeConfiguration configuration) =>
            JsonSerializer.SerializeToNode(configuration, JsonOptions) as JsonObject ?? new JsonObject();
    }

    private sealed class PluginInboundEnvelope
    {
        /// <summary>
        /// Gets or sets the sender id
        /// </summary>
        public string SenderId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender name
        /// </summary>
        public string SenderName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the chat id
        /// </summary>
        public string ChatId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the text
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the thread id
        /// </summary>
        public string ThreadId { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the reply address
        /// </summary>
        public string ReplyAddress { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether is group
        /// </summary>
        public bool IsGroup { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether is mentioned
        /// </summary>
        public bool IsMentioned { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether is reply to bot
        /// </summary>
        public bool IsReplyToBot { get; init; }

        /// <summary>
        /// Gets or sets the referenced text
        /// </summary>
        public string ReferencedText { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the image base64
        /// </summary>
        public string ImageBase64 { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the image mime type
        /// </summary>
        public string ImageMimeType { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the attachments
        /// </summary>
        public IReadOnlyList<ChannelAttachment> Attachments { get; init; } = [];
    }
}
