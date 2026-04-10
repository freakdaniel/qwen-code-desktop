using System.Collections.Concurrent;
using QwenCode.Core.Models;

namespace QwenCode.Core.Mcp;

/// <summary>
/// Represents the Mcp Connection Manager Service
/// </summary>
/// <param name="registry">The registry</param>
/// <param name="mcpToolRuntime">The mcp tool runtime</param>
/// <param name="healthOptions">The health options</param>
public sealed class McpConnectionManagerService(
    IMcpRegistry registry,
    IMcpToolRuntime mcpToolRuntime,
    McpHealthMonitorOptions? healthOptions = null) : IMcpConnectionManager
{
    private readonly McpHealthMonitorOptions health = healthOptions ?? new McpHealthMonitorOptions();
    private readonly ConcurrentDictionary<string, McpReconnectResult> states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> healthMonitors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> consecutiveFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> reconnecting = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lists servers with status
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <returns>The resulting i read only list mcp server definition</returns>
    public IReadOnlyList<McpServerDefinition> ListServersWithStatus(WorkspacePaths paths) =>
        registry.ListServers(paths)
            .Select(server =>
            {
                var key = BuildStateKey(paths, server.Name);
                if (!states.TryGetValue(key, out var state))
                {
                    return server;
                }

                return new McpServerDefinition
                {
                    Name = server.Name,
                    Scope = server.Scope,
                    Transport = server.Transport,
                    CommandOrUrl = server.CommandOrUrl,
                    Arguments = server.Arguments,
                    EnvironmentVariables = server.EnvironmentVariables,
                    Headers = server.Headers,
                    TimeoutMs = server.TimeoutMs,
                    Trust = server.Trust,
                    Description = server.Description,
                    Instructions = server.Instructions,
                    IncludeTools = server.IncludeTools,
                    ExcludeTools = server.ExcludeTools,
                    SettingsPath = server.SettingsPath,
                    HasPersistedToken = server.HasPersistedToken,
                    Status = state.Status,
                    LastReconnectAttemptUtc = state.AttemptedAtUtc,
                    LastError = state.Status == "connected" ? string.Empty : state.Message,
                    DiscoveredToolsCount = state.DiscoveredToolsCount,
                    DiscoveredPromptsCount = state.DiscoveredPromptsCount,
                    SupportsPrompts = state.SupportsPrompts,
                    SupportsResources = state.SupportsResources,
                    LastDiscoveryUtc = state.LastDiscoveryUtc
                };
            })
            .ToArray();

    /// <summary>
    /// Executes reconnect async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp reconnect result</returns>
    public async Task<McpReconnectResult> ReconnectAsync(
        WorkspacePaths paths,
        string name,
        CancellationToken cancellationToken = default)
    {
        var server = registry.ListServers(paths)
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (server is null)
        {
            return RecordState(paths, name, "missing", $"MCP server '{name}' is not configured.");
        }

        var result = await ValidateConnectionAsync(paths, server, cancellationToken);
        states[BuildStateKey(paths, name)] = result;
        if (string.Equals(result.Status, "connected", StringComparison.OrdinalIgnoreCase))
        {
            consecutiveFailures[BuildStateKey(paths, name)] = 0;
            StartHealthMonitor(paths, name);
        }
        return result;
    }

    /// <summary>
    /// Disconnects async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="name">The name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DisconnectAsync(
        WorkspacePaths paths,
        string name,
        CancellationToken cancellationToken = default)
    {
        StopHealthMonitor(paths, name);
        consecutiveFailures.TryRemove(BuildStateKey(paths, name), out _);
        reconnecting.TryRemove(BuildStateKey(paths, name), out _);
        await mcpToolRuntime.DisconnectServerAsync(paths, name, cancellationToken);
    }

    private async Task<McpReconnectResult> ValidateConnectionAsync(
        WorkspacePaths paths,
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        var attemptedAt = DateTimeOffset.UtcNow;
        try
        {
            var result = await mcpToolRuntime.ConnectServerAsync(paths, server.Name, cancellationToken);
            return new McpReconnectResult
            {
                Name = result.Name,
                Status = result.Status,
                AttemptedAtUtc = attemptedAt,
                Message = result.Message,
                DiscoveredToolsCount = result.DiscoveredToolsCount,
                DiscoveredPromptsCount = result.DiscoveredPromptsCount,
                SupportsPrompts = result.SupportsPrompts,
                SupportsResources = result.SupportsResources,
                LastDiscoveryUtc = result.LastDiscoveryUtc
            };
        }
        catch (Exception exception)
        {
            return new McpReconnectResult
            {
                Name = server.Name,
                Status = "disconnected",
                AttemptedAtUtc = attemptedAt,
                Message = exception.Message,
                DiscoveredToolsCount = 0,
                DiscoveredPromptsCount = 0
            };
        }
    }

    private void StartHealthMonitor(WorkspacePaths paths, string name)
    {
        if (!health.AutoReconnect)
        {
            return;
        }

        StopHealthMonitor(paths, name);

        var key = BuildStateKey(paths, name);
        var cancellation = new CancellationTokenSource();
        if (!healthMonitors.TryAdd(key, cancellation))
        {
            cancellation.Dispose();
            return;
        }

        _ = Task.Run(() => MonitorServerAsync(paths, name, key, cancellation.Token), CancellationToken.None);
    }

    private void StopHealthMonitor(WorkspacePaths paths, string name)
    {
        var key = BuildStateKey(paths, name);
        if (!healthMonitors.TryRemove(key, out var cancellation))
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch
        {
        }

        cancellation.Dispose();
    }

    private async Task MonitorServerAsync(
        WorkspacePaths paths,
        string name,
        string stateKey,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(health.CheckInterval, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                await PerformHealthCheckAsync(paths, name, stateKey, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PerformHealthCheckAsync(
        WorkspacePaths paths,
        string name,
        string stateKey,
        CancellationToken cancellationToken)
    {
        if (reconnecting.ContainsKey(stateKey))
        {
            return;
        }

        var server = registry.ListServers(paths)
            .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (server is null)
        {
            StopHealthMonitor(paths, name);
            states[stateKey] = new McpReconnectResult
            {
                Name = name,
                Status = "missing",
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                Message = $"MCP server '{name}' is not configured.",
                DiscoveredToolsCount = 0,
                DiscoveredPromptsCount = 0
            };
            return;
        }

        try
        {
            var result = await mcpToolRuntime.ProbeServerAsync(paths, name, cancellationToken);
            states[stateKey] = result;
            consecutiveFailures[stateKey] = 0;
        }
        catch (Exception exception)
        {
            var failures = consecutiveFailures.AddOrUpdate(stateKey, 1, static (_, current) => current + 1);
            states[stateKey] = new McpReconnectResult
            {
                Name = name,
                Status = "disconnected",
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                Message = exception.Message,
                DiscoveredToolsCount = 0,
                DiscoveredPromptsCount = 0
            };

            if (failures >= health.MaxConsecutiveFailures)
            {
                await ReconnectFromHealthMonitorAsync(paths, name, stateKey, cancellationToken);
            }
        }
    }

    private async Task ReconnectFromHealthMonitorAsync(
        WorkspacePaths paths,
        string name,
        string stateKey,
        CancellationToken cancellationToken)
    {
        if (!reconnecting.TryAdd(stateKey, 0))
        {
            return;
        }

        try
        {
            await Task.Delay(health.ReconnectDelay, cancellationToken);
            await mcpToolRuntime.DisconnectServerAsync(paths, name, cancellationToken);
            var server = registry.ListServers(paths)
                .FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (server is null)
            {
                states[stateKey] = new McpReconnectResult
                {
                    Name = name,
                    Status = "missing",
                    AttemptedAtUtc = DateTimeOffset.UtcNow,
                    Message = $"MCP server '{name}' is not configured.",
                    DiscoveredToolsCount = 0,
                    DiscoveredPromptsCount = 0
                };
                return;
            }

            var result = await ValidateConnectionAsync(paths, server, cancellationToken);
            states[stateKey] = result;
            consecutiveFailures[stateKey] = string.Equals(result.Status, "connected", StringComparison.OrdinalIgnoreCase) ? 0 : health.MaxConsecutiveFailures;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            states[stateKey] = new McpReconnectResult
            {
                Name = name,
                Status = "disconnected",
                AttemptedAtUtc = DateTimeOffset.UtcNow,
                Message = exception.Message,
                DiscoveredToolsCount = 0,
                DiscoveredPromptsCount = 0
            };
        }
        finally
        {
            reconnecting.TryRemove(stateKey, out _);
        }
    }

    private McpReconnectResult RecordState(WorkspacePaths paths, string name, string status, string message)
    {
        var state = new McpReconnectResult
        {
            Name = name,
            Status = status,
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            Message = message,
            DiscoveredToolsCount = 0,
            DiscoveredPromptsCount = 0
        };
        states[BuildStateKey(paths, name)] = state;
        return state;
    }

    private static string BuildStateKey(WorkspacePaths paths, string name) =>
        $"{Path.GetFullPath(paths.WorkspaceRoot ?? Environment.CurrentDirectory)}::{name}";
}
