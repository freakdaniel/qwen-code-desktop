namespace QwenCode.Core.Mcp;

/// <summary>
/// Represents the MCP Resource Registry Service.
/// </summary>
/// <param name="connectionManager">The connection manager.</param>
/// <param name="mcpToolRuntime">The mcp tool runtime.</param>
public sealed class McpResourceRegistryService(
    IMcpConnectionManager connectionManager,
    IMcpToolRuntime mcpToolRuntime) : IMcpResourceRegistryService
{
    /// <summary>
    /// Gets snapshot async.
    /// </summary>
    /// <param name="paths">The paths to process.</param>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to mcp resource registry snapshot.</returns>
    public async Task<McpResourceRegistrySnapshot> GetSnapshotAsync(
        WorkspacePaths paths,
        GetMcpResourceRegistryRequest request,
        CancellationToken cancellationToken = default)
    {
        var servers = connectionManager.ListServersWithStatus(paths)
            .Where(server =>
                string.IsNullOrWhiteSpace(request.ServerName) ||
                string.Equals(server.Name, request.ServerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var entries = new List<McpResourceRegistryEntry>();
        foreach (var server in servers)
        {
            try
            {
                if (request.ForceRefresh && !string.Equals(server.Status, "connected", StringComparison.OrdinalIgnoreCase))
                {
                    await connectionManager.ReconnectAsync(paths, server.Name, cancellationToken);
                }

                var resources = await mcpToolRuntime.ListResourcesAsync(paths, server.Name, cancellationToken);
                foreach (var resource in resources)
                {
                    entries.Add(new McpResourceRegistryEntry
                    {
                        Name = NormalizeName(resource.Name, resource.Uri),
                        Uri = resource.Uri,
                        QualifiedName = $"{server.Name}/{resource.Uri}",
                        ServerName = server.Name,
                        Description = resource.Description,
                        MimeType = resource.MimeType,
                        Source = "mcp"
                    });
                }
            }
            catch
            {
                // Keep the registry browseable even when one server is offline or unauthorised.
            }
        }

        var deduplicated = Deduplicate(entries);

        return new McpResourceRegistrySnapshot
        {
            TotalCount = deduplicated.Count,
            ServerCount = deduplicated.Select(static item => item.ServerName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Resources = deduplicated
        };
    }

    /// <summary>
    /// Reads async.
    /// </summary>
    /// <param name="paths">The paths to process.</param>
    /// <param name="request">The request payload.</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation.</param>
    /// <returns>A task that resolves to mcp resource read result.</returns>
    public async Task<McpResourceReadResult> ReadAsync(
        WorkspacePaths paths,
        ReadMcpResourceRegistryEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("MCP resource name is required.");
        }

        var snapshot = await GetSnapshotAsync(paths, new GetMcpResourceRegistryRequest(), cancellationToken);
        var resource = snapshot.Resources.FirstOrDefault(item =>
            string.Equals(item.Name, request.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.QualifiedName, request.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Uri, request.Name, StringComparison.OrdinalIgnoreCase));
        if (resource is null)
        {
            throw new InvalidOperationException($"MCP resource '{request.Name}' was not found.");
        }

        return await mcpToolRuntime.ReadResourceAsync(paths, resource.ServerName, resource.Uri, cancellationToken);
    }

    private static IReadOnlyList<McpResourceRegistryEntry> Deduplicate(IReadOnlyList<McpResourceRegistryEntry> entries)
    {
        var groups = entries
            .GroupBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var results = new List<McpResourceRegistryEntry>(entries.Count);

        foreach (var group in groups)
        {
            if (group.Count() == 1)
            {
                results.Add(group.First());
                continue;
            }

            foreach (var entry in group.OrderBy(static item => item.ServerName, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(new McpResourceRegistryEntry
                {
                    Name = $"{entry.ServerName}_{entry.Name}",
                    Uri = entry.Uri,
                    QualifiedName = entry.QualifiedName,
                    ServerName = entry.ServerName,
                    Description = entry.Description,
                    MimeType = entry.MimeType,
                    Source = entry.Source
                });
            }
        }

        return results
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeName(string name, string uri)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            var segment = parsed.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrWhiteSpace(segment))
            {
                return segment;
            }
        }

        return uri.Replace("://", "_", StringComparison.Ordinal).Replace('/', '_').Replace('\\', '_');
    }
}
