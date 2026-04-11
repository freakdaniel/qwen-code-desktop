using QwenCode.Core.Models;

namespace QwenCode.Core.Ide;

/// <summary>
/// Represents the Ide Client Service
/// </summary>
/// <param name="backendService">The backend service</param>
/// <param name="transport">The transport</param>
public sealed class IdeClientService(
    IIdeBackendService backendService,
    IIdeCompanionTransport transport) : IIdeClientService
{
    private readonly Lock gate = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IdeDiffUpdateResult>> pendingDiffs = new(StringComparer.OrdinalIgnoreCase);
    private IdeConnectionSnapshot snapshot = new()
    {
        Status = "disconnected",
        Details = "IDE client is not connected."
    };

    /// <summary>
    /// Gets snapshot
    /// </summary>
    /// <returns>The resulting ide connection snapshot</returns>
    public IdeConnectionSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return snapshot;
        }
    }

    /// <summary>
    /// Connects async
    /// </summary>
    /// <param name="workspaceRoot">The workspace root</param>
    /// <param name="processCommand">The process command</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide connection snapshot</returns>
    public async Task<IdeConnectionSnapshot> ConnectAsync(
        string workspaceRoot,
        string processCommand = "",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var inspected = backendService.Inspect(workspaceRoot, processCommand);
        if (!string.Equals(inspected.Status, "connected", StringComparison.OrdinalIgnoreCase))
        {
            lock (gate)
            {
                snapshot = inspected;
            }

            return inspected;
        }

        var connection = backendService.ResolveTransportConnection(workspaceRoot, processCommand);
        if (connection is null)
        {
            var disconnected = Clone(inspected, status: "disconnected", details: "Failed to resolve a valid IDE companion connection.");
            lock (gate)
            {
                snapshot = disconnected;
            }

            return disconnected;
        }

        var connected = await transport.ConnectAsync(connection, cancellationToken);
        if (!connected)
        {
            var disconnected = Clone(inspected, status: "disconnected", details: "Failed to connect to the IDE companion transport.");
            lock (gate)
            {
                snapshot = disconnected;
            }

            return disconnected;
        }

        var tools = await transport.ListToolsAsync(cancellationToken);
        var resolved = Clone(
            inspected,
            status: "connected",
            details: string.Empty,
            availableTools: tools,
            supportsDiff: tools.Contains("openDiff", StringComparer.OrdinalIgnoreCase) &&
                          tools.Contains("closeDiff", StringComparer.OrdinalIgnoreCase));

        lock (gate)
        {
            snapshot = resolved;
        }

        return resolved;
    }

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await transport.DisconnectAsync(cancellationToken);
        foreach (var pending in pendingDiffs)
        {
            pending.Value.TrySetResult(new IdeDiffUpdateResult
            {
                Status = "rejected"
            });
        }

        pendingDiffs.Clear();

        lock (gate)
        {
            snapshot = Clone(snapshot, status: "disconnected", details: "IDE integration disabled. To enable it again, reconnect the IDE backend.");
        }
    }

    /// <summary>
    /// Updates context
    /// </summary>
    /// <param name="input">The input</param>
    /// <returns>The resulting ide context snapshot</returns>
    public IdeContextSnapshot UpdateContext(IdeContextSnapshot input)
    {
        var normalized = backendService.UpdateContext(input);
        lock (gate)
        {
            snapshot = Clone(snapshot, context: normalized);
            return snapshot.Context!;
        }
    }

    /// <summary>
    /// Opens diff async
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="newContent">The new content</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to ide diff update result</returns>
    public async Task<IdeDiffUpdateResult> OpenDiffAsync(
        string filePath,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        EnsureDiffingEnabled();

        var resolver = new TaskCompletionSource<IdeDiffUpdateResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingDiffs[filePath] = resolver;

        var result = await transport.CallToolAsync(
            "openDiff",
            new JsonObject
            {
                ["filePath"] = filePath,
                ["newContent"] = newContent
            },
            cancellationToken);

        if (result.IsError)
        {
            pendingDiffs.TryRemove(filePath, out _);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Text) ? "openDiff failed." : result.Text);
        }

        using var registration = cancellationToken.Register(() => resolver.TrySetCanceled(cancellationToken));
        return await resolver.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Closes diff async
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="suppressNotification">The suppress notification</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to string?</returns>
    public async Task<string?> CloseDiffAsync(
        string filePath,
        bool suppressNotification = false,
        CancellationToken cancellationToken = default)
    {
        EnsureConnected();

        var result = await transport.CallToolAsync(
            "closeDiff",
            new JsonObject
            {
                ["filePath"] = filePath,
                ["suppressNotification"] = suppressNotification
            },
            cancellationToken);

        if (result.IsError)
        {
            return null;
        }

        return TryExtractContent(result.Text);
    }

    /// <summary>
    /// Resolves diff from cli async
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <param name="outcome">The outcome</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task ResolveDiffFromCliAsync(
        string filePath,
        string outcome,
        CancellationToken cancellationToken = default)
    {
        var normalizedOutcome = outcome.Trim().ToLowerInvariant();
        if (normalizedOutcome is not ("accepted" or "rejected"))
        {
            throw new InvalidOperationException("Diff outcome must be either 'accepted' or 'rejected'.");
        }

        var content = await CloseDiffAsync(filePath, suppressNotification: true, cancellationToken);
        if (pendingDiffs.TryRemove(filePath, out var resolver))
        {
            resolver.TrySetResult(new IdeDiffUpdateResult
            {
                Status = normalizedOutcome,
                Content = normalizedOutcome == "accepted" ? content ?? string.Empty : string.Empty
            });
        }
    }

    private void EnsureConnected()
    {
        var current = GetSnapshot();
        if (!string.Equals(current.Status, "connected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("IDE client is not connected.");
        }
    }

    private void EnsureDiffingEnabled()
    {
        var current = GetSnapshot();
        if (!string.Equals(current.Status, "connected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("IDE client is not connected.");
        }

        if (!current.SupportsDiff)
        {
            throw new InvalidOperationException("IDE companion does not expose openDiff and closeDiff tools.");
        }
    }

    private static string? TryExtractContent(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("content", out var contentProperty))
            {
                return contentProperty.ValueKind == JsonValueKind.Null
                    ? null
                    : contentProperty.GetString();
            }
        }
        catch
        {
        }

        return payload;
    }

    private static IdeConnectionSnapshot Clone(
        IdeConnectionSnapshot source,
        string? status = null,
        string? details = null,
        IReadOnlyList<string>? availableTools = null,
        bool? supportsDiff = null,
        IdeContextSnapshot? context = null) =>
        new()
        {
            Status = status ?? source.Status,
            Details = details ?? source.Details,
            Ide = source.Ide,
            WorkspacePath = source.WorkspacePath,
            Port = source.Port,
            Command = source.Command,
            AuthToken = source.AuthToken,
            SupportsDiff = supportsDiff ?? source.SupportsDiff,
            AvailableTools = availableTools ?? source.AvailableTools,
            Context = context ?? source.Context
        };
}
