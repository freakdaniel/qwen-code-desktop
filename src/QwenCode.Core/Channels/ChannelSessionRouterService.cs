using System.Text.Json;
using QwenCode.Core.Infrastructure;
using QwenCode.Core.Models;

namespace QwenCode.Core.Channels;

/// <summary>
/// Represents the Channel Session Router Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
public sealed class ChannelSessionRouterService(IDesktopEnvironmentPaths environmentPaths) : IChannelSessionRouter
{
    private readonly object gate = new();
    private readonly Dictionary<string, ChannelSessionRoute> routes = new(StringComparer.OrdinalIgnoreCase);
    private bool loaded;

    /// <summary>
    /// Resolves async
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="sessionScope">The session scope</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="chatId">The chat id</param>
    /// <param name="threadId">The thread id</param>
    /// <param name="replyAddress">The reply address</param>
    /// <param name="workingDirectory">The working directory</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to channel session route</returns>
    public Task<ChannelSessionRoute> ResolveAsync(
        string channelName,
        string sessionScope,
        string senderId,
        string chatId,
        string threadId,
        string replyAddress,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLoaded();

        lock (gate)
        {
            var key = BuildKey(channelName, sessionScope, senderId, chatId, threadId);
            if (routes.TryGetValue(key, out var existing))
            {
                if (!string.IsNullOrWhiteSpace(replyAddress) &&
                    !string.Equals(existing.ReplyAddress, replyAddress, StringComparison.Ordinal))
                {
                    existing = new ChannelSessionRoute
                    {
                        SessionId = existing.SessionId,
                        ChannelName = existing.ChannelName,
                        SenderId = existing.SenderId,
                        ChatId = existing.ChatId,
                        ThreadId = existing.ThreadId,
                        ReplyAddress = replyAddress,
                        WorkingDirectory = existing.WorkingDirectory
                    };
                    routes[key] = existing;
                    PersistUnsafe();
                }

                return Task.FromResult(existing);
            }

            var created = new ChannelSessionRoute
            {
                SessionId = Guid.NewGuid().ToString(),
                ChannelName = channelName,
                SenderId = senderId,
                ChatId = chatId,
                ThreadId = threadId,
                ReplyAddress = replyAddress,
                WorkingDirectory = workingDirectory
            };
            routes[key] = created;
            PersistUnsafe();
            return Task.FromResult(created);
        }
    }

    /// <summary>
    /// Executes has session
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="chatId">The chat id</param>
    /// <returns>A value indicating whether the operation succeeded</returns>
    public bool HasSession(string channelName, string senderId, string chatId = "")
    {
        EnsureLoaded();

        lock (gate)
        {
            if (!string.IsNullOrWhiteSpace(chatId))
            {
                return routes.Values.Any(route =>
                    string.Equals(route.ChannelName, channelName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(route.SenderId, senderId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(route.ChatId, chatId, StringComparison.OrdinalIgnoreCase));
            }

            return routes.Values.Any(route =>
                string.Equals(route.ChannelName, channelName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(route.SenderId, senderId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Removes sessions
    /// </summary>
    /// <param name="channelName">The channel name</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="chatId">The chat id</param>
    /// <returns>The resulting i read only list string</returns>
    public IReadOnlyList<string> RemoveSessions(string channelName, string senderId, string chatId = "")
    {
        EnsureLoaded();

        lock (gate)
        {
            var removedKeys = routes
                .Where(item =>
                    string.Equals(item.Value.ChannelName, channelName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(item.Value.SenderId, senderId, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(chatId) ||
                     string.Equals(item.Value.ChatId, chatId, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Key)
                .ToArray();

            if (removedKeys.Length == 0)
            {
                return [];
            }

            var sessionIds = new List<string>(removedKeys.Length);
            foreach (var key in removedKeys)
            {
                if (routes.Remove(key, out var route))
                {
                    sessionIds.Add(route.SessionId);
                }
            }

            PersistUnsafe();
            return sessionIds;
        }
    }

    /// <summary>
    /// Lists routes
    /// </summary>
    /// <returns>The resulting i read only list channel session route</returns>
    public IReadOnlyList<ChannelSessionRoute> ListRoutes()
    {
        EnsureLoaded();
        lock (gate)
        {
            return routes.Values
                .OrderBy(static item => item.ChannelName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static item => item.ChatId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>
    /// Executes clear
    /// </summary>
    public void Clear()
    {
        EnsureLoaded();
        lock (gate)
        {
            routes.Clear();
            var path = GetPersistPath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        lock (gate)
        {
            if (loaded)
            {
                return;
            }

            var path = GetPersistPath();
            var legacyPath = GetLegacyPersistPath();
            var effectivePath = File.Exists(path) ? path : legacyPath;
            if (File.Exists(effectivePath))
            {
                try
                {
                    var persisted = JsonSerializer.Deserialize<Dictionary<string, ChannelSessionRoute>>(File.ReadAllText(effectivePath));
                    if (persisted is not null)
                    {
                        foreach (var item in persisted)
                        {
                            routes[item.Key] = item.Value;
                        }
                    }
                }
                catch
                {
                    routes.Clear();
                }
            }

            loaded = true;
        }
    }

    private void PersistUnsafe()
    {
        var path = GetPersistPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(routes, new JsonSerializerOptions { WriteIndented = true }));
    }

    private string GetPersistPath() => Path.Combine(environmentPaths.HomeDirectory, ".qwen", "channels", "sessions.json");

    private string GetLegacyPersistPath() => Path.Combine(environmentPaths.HomeDirectory, ".qwen", "channels", "sessions.runtime.json");

    private static string BuildKey(
        string channelName,
        string sessionScope,
        string senderId,
        string chatId,
        string threadId) =>
        sessionScope.ToLowerInvariant() switch
        {
            "thread" => $"{channelName}:{threadId}:{chatId}",
            "single" => $"{channelName}:__single__",
            _ => $"{channelName}:{senderId}:{chatId}"
        };
}
