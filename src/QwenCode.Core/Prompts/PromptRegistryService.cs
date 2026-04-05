using System.Text.Json;
using QwenCode.App.Mcp;
using QwenCode.App.Models;

namespace QwenCode.App.Prompts;

/// <summary>
/// Represents the Prompt Registry Service
/// </summary>
/// <param name="connectionManager">The connection manager</param>
/// <param name="mcpToolRuntime">The mcp tool runtime</param>
public sealed class PromptRegistryService(
    IMcpConnectionManager connectionManager,
    IMcpToolRuntime mcpToolRuntime) : IPromptRegistryService
{
    /// <summary>
    /// Gets snapshot async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to prompt registry snapshot</returns>
    public async Task<PromptRegistrySnapshot> GetSnapshotAsync(
        WorkspacePaths paths,
        GetPromptRegistryRequest request,
        CancellationToken cancellationToken = default)
    {
        var servers = connectionManager.ListServersWithStatus(paths)
            .Where(server =>
                string.IsNullOrWhiteSpace(request.ServerName) ||
                string.Equals(server.Name, request.ServerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var entries = new List<PromptRegistryEntry>();
        foreach (var server in servers)
        {
            try
            {
                if (request.ForceRefresh && !string.Equals(server.Status, "connected", StringComparison.OrdinalIgnoreCase))
                {
                    await connectionManager.ReconnectAsync(paths, server.Name, cancellationToken);
                }

                var prompts = await mcpToolRuntime.ListPromptsAsync(paths, server.Name, cancellationToken);
                foreach (var prompt in prompts)
                {
                    entries.Add(new PromptRegistryEntry
                    {
                        Name = prompt.Name,
                        PromptName = prompt.Name,
                        QualifiedName = $"{server.Name}/{prompt.Name}",
                        ServerName = server.Name,
                        Description = prompt.Description,
                        ArgumentsJson = prompt.ArgumentsJson,
                        Source = "mcp"
                    });
                }
            }
            catch
            {
                // Ignore unavailable servers and keep registry focused on discovered prompts.
            }
        }

        var deduplicated = Deduplicate(entries);

        return new PromptRegistrySnapshot
        {
            TotalCount = deduplicated.Count,
            ServerCount = deduplicated.Select(static item => item.ServerName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Prompts = deduplicated
        };
    }

    /// <summary>
    /// Invokes async
    /// </summary>
    /// <param name="paths">The paths to process</param>
    /// <param name="request">The request payload</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to mcp prompt invocation result</returns>
    public async Task<McpPromptInvocationResult> InvokeAsync(
        WorkspacePaths paths,
        InvokePromptRegistryEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Prompt name is required.");
        }

        var snapshot = await GetSnapshotAsync(paths, new GetPromptRegistryRequest(), cancellationToken);
        var prompt = snapshot.Prompts.FirstOrDefault(item =>
            string.Equals(item.Name, request.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.QualifiedName, request.Name, StringComparison.OrdinalIgnoreCase));
        if (prompt is null)
        {
            throw new InvalidOperationException($"Registered prompt '{request.Name}' was not found.");
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson);
        return await mcpToolRuntime.GetPromptAsync(
            paths,
            prompt.ServerName,
            prompt.PromptName,
            document.RootElement,
            cancellationToken);
    }

    private static IReadOnlyList<PromptRegistryEntry> Deduplicate(IReadOnlyList<PromptRegistryEntry> entries)
    {
        var groups = entries
            .GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<PromptRegistryEntry>(entries.Count);

        foreach (var group in groups)
        {
            if (group.Count() == 1)
            {
                results.Add(group.First());
                continue;
            }

            foreach (var entry in group.OrderBy(static item => item.ServerName, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(new PromptRegistryEntry
                {
                    Name = $"{entry.ServerName}_{entry.PromptName}",
                    PromptName = entry.PromptName,
                    QualifiedName = entry.QualifiedName,
                    ServerName = entry.ServerName,
                    Description = entry.Description,
                    ArgumentsJson = entry.ArgumentsJson,
                    Source = entry.Source
                });
            }
        }

        return results
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
