using System.Collections.Concurrent;
using QwenCode.App.Models;

namespace QwenCode.App.Mcp;

public sealed class McpConnectionManagerService(
    IMcpRegistry registry,
    HttpClient httpClient) : IMcpConnectionManager
{
    private readonly ConcurrentDictionary<string, McpReconnectResult> states = new(StringComparer.OrdinalIgnoreCase);

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
                    IncludeTools = server.IncludeTools,
                    ExcludeTools = server.ExcludeTools,
                    SettingsPath = server.SettingsPath,
                    HasPersistedToken = server.HasPersistedToken,
                    Status = state.Status,
                    LastReconnectAttemptUtc = state.AttemptedAtUtc,
                    LastError = state.Status == "connected" ? string.Empty : state.Message
                };
            })
            .ToArray();

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

        var result = await ValidateConnectionAsync(server, cancellationToken);
        states[BuildStateKey(paths, name)] = result;
        return result;
    }

    private async Task<McpReconnectResult> ValidateConnectionAsync(
        McpServerDefinition server,
        CancellationToken cancellationToken)
    {
        var attemptedAt = DateTimeOffset.UtcNow;
        try
        {
            return server.Transport.ToLowerInvariant() switch
            {
                "http" or "sse" => await ValidateHttpTransportAsync(server, attemptedAt, cancellationToken),
                _ => ValidateStdioTransport(server, attemptedAt)
            };
        }
        catch (Exception exception)
        {
            return new McpReconnectResult
            {
                Name = server.Name,
                Status = "disconnected",
                AttemptedAtUtc = attemptedAt,
                Message = exception.Message
            };
        }
    }

    private async Task<McpReconnectResult> ValidateHttpTransportAsync(
        McpServerDefinition server,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(server.CommandOrUrl, UriKind.Absolute, out var uri))
        {
            return new McpReconnectResult
            {
                Name = server.Name,
                Status = "disconnected",
                AttemptedAtUtc = attemptedAtUtc,
                Message = "MCP server URL is invalid."
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
        foreach (var header in server.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(server.TimeoutMs ?? 2000));

        try
        {
            using var response = await httpClient.SendAsync(request, timeoutSource.Token);
            return new McpReconnectResult
            {
                Name = server.Name,
                Status = response.IsSuccessStatusCode ? "connected" : "disconnected",
                AttemptedAtUtc = attemptedAtUtc,
                Message = $"MCP {server.Transport} endpoint responded with {(int)response.StatusCode}."
            };
        }
        catch (HttpRequestException exception)
        {
            return new McpReconnectResult
            {
                Name = server.Name,
                Status = "disconnected",
                AttemptedAtUtc = attemptedAtUtc,
                Message = exception.Message
            };
        }
    }

    private static McpReconnectResult ValidateStdioTransport(
        McpServerDefinition server,
        DateTimeOffset attemptedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(server.CommandOrUrl))
        {
            return new McpReconnectResult
            {
                Name = server.Name,
                Status = "disconnected",
                AttemptedAtUtc = attemptedAtUtc,
                Message = "MCP stdio command is missing."
            };
        }

        var isExecutableAvailable = Path.IsPathRooted(server.CommandOrUrl)
            ? File.Exists(server.CommandOrUrl)
            : ResolveCommandPath(server.CommandOrUrl) is not null;

        return new McpReconnectResult
        {
            Name = server.Name,
            Status = isExecutableAvailable ? "connected" : "disconnected",
            AttemptedAtUtc = attemptedAtUtc,
            Message = isExecutableAvailable
                ? $"MCP stdio command '{server.CommandOrUrl}' is available."
                : $"MCP stdio command '{server.CommandOrUrl}' was not found."
        };
    }

    private McpReconnectResult RecordState(WorkspacePaths paths, string name, string status, string message)
    {
        var state = new McpReconnectResult
        {
            Name = name,
            Status = status,
            AttemptedAtUtc = DateTimeOffset.UtcNow,
            Message = message
        };
        states[BuildStateKey(paths, name)] = state;
        return state;
    }

    private static string BuildStateKey(WorkspacePaths paths, string name) =>
        $"{Path.GetFullPath(paths.WorkspaceRoot ?? Environment.CurrentDirectory)}::{name}";

    private static string? ResolveCommandPath(string command)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var candidates = OperatingSystem.IsWindows()
            ? new[] { command, $"{command}.exe", $"{command}.cmd", $"{command}.bat" }
            : [command];

        foreach (var segment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidate in candidates)
            {
                var fullPath = Path.Combine(segment, candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }
}
