using System.Diagnostics;
using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Config;
using QwenCode.App.Extensions;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

/// <summary>
/// Represents the Channel Registry Service
/// </summary>
/// <param name="environmentPaths">The environment paths</param>
/// <param name="settingsResolver">The settings resolver</param>
/// <param name="extensionCatalogService">The extension catalog service</param>
/// <param name="configService">The config service</param>
public sealed class ChannelRegistryService(
    IDesktopEnvironmentPaths environmentPaths,
    ISettingsResolver settingsResolver,
    IExtensionCatalogService extensionCatalogService,
    IConfigService? configService = null) : IChannelRegistryService
{
    private const int PairingExpiryMinutes = 60;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] BuiltInTypes = ["telegram", "weixin", "dingtalk"];
    private readonly IConfigService config = configService ?? new RuntimeConfigService(environmentPaths);

    /// <summary>
    /// Executes inspect
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <returns>The resulting channel snapshot</returns>
    public ChannelSnapshot Inspect(WorkspacePaths workspace)
    {
        var runtime = settingsResolver.InspectRuntimeProfile(workspace);
        var serviceInfo = ReadServiceInfo(GetChannelsRoot());
        var sessionCounts = ReadSessionCounts(GetChannelsRoot());
        var configuredChannels = ReadConfiguredChannels(runtime, workspace);
        var extensionSnapshot = extensionCatalogService.Inspect(workspace);
        var supportedTypes = BuiltInTypes
            .Concat(extensionSnapshot.Extensions.SelectMany(static item => item.Channels))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var channels = configuredChannels
            .Select(channel =>
            {
                var pendingPairings = ReadPendingPairings(GetChannelsRoot(), channel.Name);
                return new ChannelDefinition
                {
                    Name = channel.Name,
                    Type = channel.Type,
                    Scope = channel.Scope,
                    Description = channel.Description,
                    SenderPolicy = channel.SenderPolicy,
                    SessionScope = channel.SessionScope,
                    WorkingDirectory = channel.WorkingDirectory,
                    ApprovalMode = channel.ApprovalMode,
                    Model = channel.Model,
                    Status = serviceInfo?.Channels.Contains(channel.Name, StringComparer.OrdinalIgnoreCase) == true
                        ? "running"
                        : "configured",
                    SupportsPairing = string.Equals(channel.SenderPolicy, "pairing", StringComparison.OrdinalIgnoreCase),
                    SessionCount = sessionCounts.TryGetValue(channel.Name, out var count) ? count : 0,
                    PendingPairingCount = pendingPairings.Count,
                    AllowlistCount = ReadAllowlist(GetChannelsRoot(), channel.Name).Count
                };
            })
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ChannelSnapshot
        {
            IsServiceRunning = serviceInfo is not null,
            ServiceProcessId = serviceInfo?.Pid,
            ServiceStartedAtUtc = serviceInfo?.StartedAt ?? string.Empty,
            ServiceUptimeText = serviceInfo is null ? string.Empty : FormatUptime(serviceInfo.StartedAt),
            SupportedTypes = supportedTypes,
            Channels = channels
        };
    }

    /// <summary>
    /// Gets channel
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="name">The name</param>
    /// <returns>The resulting channel definition</returns>
    public ChannelDefinition GetChannel(WorkspacePaths workspace, string name) =>
        Inspect(workspace).Channels.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Channel \"{name}\" was not found in merged qwen settings.");

    /// <summary>
    /// Gets runtime configuration
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="name">The name</param>
    /// <returns>The resulting channel runtime configuration</returns>
    public ChannelRuntimeConfiguration GetRuntimeConfiguration(WorkspacePaths workspace, string name)
    {
        var runtime = settingsResolver.InspectRuntimeProfile(workspace);
        return ReadConfiguredChannels(runtime, workspace)
            .Where(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(static item => new ChannelRuntimeConfiguration
            {
                Name = item.Name,
                Type = item.Type,
                SenderPolicy = item.SenderPolicy,
                SessionScope = item.SessionScope,
                WorkingDirectory = item.WorkingDirectory,
                ApprovalMode = item.ApprovalMode,
                Model = item.Model,
                Token = item.Token,
                ClientId = item.ClientId,
                ClientSecret = item.ClientSecret,
                BaseUrl = item.BaseUrl,
                Instructions = item.Instructions,
                GroupPolicy = item.GroupPolicy,
                DispatchMode = item.DispatchMode,
                BlockStreaming = item.BlockStreaming,
                BlockStreamingChunk = item.BlockStreamingChunk,
                BlockStreamingCoalesce = item.BlockStreamingCoalesce,
                RequireMentionByDefault = item.RequireMentionByDefault,
                Groups = item.Groups,
                AdditionalSettings = item.AdditionalSettings.ToDictionary(
                    static entry => entry.Key,
                    static entry => entry.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase)
            })
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Channel \"{name}\" was not found in merged qwen settings.");
    }

    /// <summary>
    /// Executes evaluate sender access
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="channelName">The channel name</param>
    /// <param name="senderId">The sender id</param>
    /// <param name="senderName">The sender name</param>
    /// <returns>The resulting channel sender access decision</returns>
    public ChannelSenderAccessDecision EvaluateSenderAccess(WorkspacePaths workspace, string channelName, string senderId, string senderName)
    {
        var channel = GetChannel(workspace, channelName);
        var channelsRoot = GetChannelsRoot();
        var allowlist = ReadAllowlist(channelsRoot, channelName);
        if (allowlist.Contains(senderId, StringComparer.OrdinalIgnoreCase))
        {
            return new ChannelSenderAccessDecision
            {
                Allowed = true
            };
        }

        if (string.Equals(channel.SenderPolicy, "open", StringComparison.OrdinalIgnoreCase))
        {
            return new ChannelSenderAccessDecision
            {
                Allowed = true
            };
        }

        if (string.Equals(channel.SenderPolicy, "allowlist", StringComparison.OrdinalIgnoreCase))
        {
            return new ChannelSenderAccessDecision
            {
                Allowed = false,
                Reason = $"Sender '{senderId}' is not in the allowlist for channel '{channelName}'."
            };
        }

        var pending = ReadPendingPairings(channelsRoot, channelName);
        var existing = pending.FirstOrDefault(item => string.Equals(item.SenderId, senderId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return new ChannelSenderAccessDecision
            {
                Allowed = false,
                PairingCode = existing.Code,
                Reason = $"Sender '{senderId}' must complete pairing with code '{existing.Code}'."
            };
        }

        var created = new PairingRequestRecord
        {
            SenderId = senderId,
            SenderName = senderName,
            Code = GeneratePairingCode(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        pending.Add(created);
        WritePendingPairings(channelsRoot, channelName, pending);
        return new ChannelSenderAccessDecision
        {
            Allowed = false,
            PairingCode = created.Code,
            Reason = $"Sender '{senderId}' must complete pairing with code '{created.Code}'."
        };
    }

    /// <summary>
    /// Gets pairings
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting channel pairing snapshot</returns>
    public ChannelPairingSnapshot GetPairings(WorkspacePaths workspace, GetChannelPairingRequest request)
    {
        var runtime = settingsResolver.InspectRuntimeProfile(workspace);
        EnsureChannelExists(runtime, workspace, request.Name);
        return BuildPairingSnapshot(request.Name);
    }

    /// <summary>
    /// Approves pairing
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="request">The request payload</param>
    /// <returns>The resulting channel pairing snapshot</returns>
    public ChannelPairingSnapshot ApprovePairing(WorkspacePaths workspace, ApproveChannelPairingRequest request)
    {
        var runtime = settingsResolver.InspectRuntimeProfile(workspace);
        EnsureChannelExists(runtime, workspace, request.Name);

        var channelsRoot = GetChannelsRoot();
        var pending = ReadPendingPairings(channelsRoot, request.Name);
        var requestCode = request.Code.Trim().ToUpperInvariant();
        var approved = pending.FirstOrDefault(item => string.Equals(item.Code, requestCode, StringComparison.Ordinal));
        if (approved is null)
        {
            throw new InvalidOperationException($"No pending pairing request found for code \"{requestCode}\".");
        }

        var remaining = pending
            .Where(item => !string.Equals(item.Code, requestCode, StringComparison.Ordinal))
            .ToArray();
        WritePendingPairings(channelsRoot, request.Name, remaining);

        var allowlist = ReadAllowlist(channelsRoot, request.Name);
        if (!allowlist.Contains(approved.SenderId, StringComparer.Ordinal))
        {
            allowlist.Add(approved.SenderId);
            WriteAllowlist(channelsRoot, request.Name, allowlist);
        }

        return BuildPairingSnapshot(request.Name);
    }

    private ChannelPairingSnapshot BuildPairingSnapshot(string channelName)
    {
        var channelsRoot = GetChannelsRoot();
        var pending = ReadPendingPairings(channelsRoot, channelName)
            .Select(item => new ChannelPairingRequest
            {
                SenderId = item.SenderId,
                SenderName = item.SenderName,
                Code = item.Code,
                CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(item.CreatedAt).UtcDateTime.ToString("O"),
                MinutesAgo = Math.Max(0, (int)Math.Round((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - item.CreatedAt) / 60000d))
            })
            .OrderBy(static item => item.MinutesAgo)
            .ToArray();

        var allowlist = ReadAllowlist(channelsRoot, channelName);

        return new ChannelPairingSnapshot
        {
            ChannelName = channelName,
            PendingCount = pending.Length,
            AllowlistCount = allowlist.Count,
            PendingRequests = pending
        };
    }

    private void EnsureChannelExists(QwenRuntimeProfile runtime, WorkspacePaths workspace, string name)
    {
        var exists = ReadConfiguredChannels(runtime, workspace)
            .Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (!exists)
        {
            throw new InvalidOperationException($"Channel \"{name}\" was not found in merged qwen settings.");
        }
    }

    private IReadOnlyList<ConfiguredChannel> ReadConfiguredChannels(QwenRuntimeProfile runtime, WorkspacePaths workspace)
    {
        var snapshot = config.Inspect(workspace);
        var layers = snapshot.SettingsLayers
            .Where(static layer => layer.Included)
            .Select(static layer => (layer.Path, layer.Scope))
            .ToArray();

        var merged = new Dictionary<string, ChannelConfigAccumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in layers.Where(static item => File.Exists(item.Path)))
        {
            try
            {
                using var stream = File.OpenRead(layer.Path);
                using var document = JsonDocument.Parse(
                    stream,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true,
                        CommentHandling = JsonCommentHandling.Skip
                    });

                if (!document.RootElement.TryGetProperty("channels", out var channelsElement) ||
                    channelsElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var channelProperty in channelsElement.EnumerateObject())
                {
                    if (channelProperty.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!merged.TryGetValue(channelProperty.Name, out var accumulator))
                    {
                        accumulator = new ChannelConfigAccumulator
                        {
                            Name = channelProperty.Name
                        };
                        merged[channelProperty.Name] = accumulator;
                    }

                    accumulator.Scope = layer.Scope;

                    foreach (var property in channelProperty.Value.EnumerateObject())
                    {
                        accumulator.Properties[property.Name] = property.Value.Clone();
                    }
                }
            }
            catch
            {
                // Keep behavior best-effort across malformed layers.
            }
        }

        return merged.Values
            .Select(item => item.ToConfiguredChannel(runtime.ProjectRoot))
            .Where(static item => !string.IsNullOrWhiteSpace(item.Type))
            .ToArray();
    }

    private string GetChannelsRoot() => Path.Combine(environmentPaths.HomeDirectory, ".qwen", "channels");

    private static ChannelServiceInfo? ReadServiceInfo(string channelsRoot)
    {
        var path = Path.Combine(channelsRoot, "service.pid");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var info = JsonSerializer.Deserialize<ChannelServiceInfo>(File.ReadAllText(path), JsonOptions);
            if (info is null || !IsProcessAlive(info.Pid))
            {
                TryDelete(path);
                return null;
            }

            return info;
        }
        catch
        {
            TryDelete(path);
            return null;
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, int> ReadSessionCounts(string channelsRoot)
    {
        var sessionsPath = Path.Combine(channelsRoot, "sessions.json");
        if (!File.Exists(sessionsPath))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(sessionsPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                string? channelName;
                if (entry.Value.TryGetProperty("target", out var target) &&
                    target.ValueKind == JsonValueKind.Object &&
                    target.TryGetProperty("channelName", out var targetChannelNameElement) &&
                    targetChannelNameElement.ValueKind == JsonValueKind.String)
                {
                    channelName = targetChannelNameElement.GetString();
                }
                else if (entry.Value.TryGetProperty("channelName", out var directChannelNameElement) &&
                         directChannelNameElement.ValueKind == JsonValueKind.String)
                {
                    channelName = directChannelNameElement.GetString();
                }
                else
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(channelName))
                {
                    continue;
                }

                counts[channelName] = counts.TryGetValue(channelName, out var current) ? current + 1 : 1;
            }

            return counts;
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static List<PairingRequestRecord> ReadPendingPairings(string channelsRoot, string channelName)
    {
        var path = Path.Combine(channelsRoot, $"{channelName}-pairing.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var records = JsonSerializer.Deserialize<List<PairingRequestRecord>>(File.ReadAllText(path), JsonOptions) ?? [];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return records
                .Where(item => now - item.CreatedAt < TimeSpan.FromMinutes(PairingExpiryMinutes).TotalMilliseconds)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static void WritePendingPairings(
        string channelsRoot,
        string channelName,
        IReadOnlyList<PairingRequestRecord> requests)
    {
        Directory.CreateDirectory(channelsRoot);
        var path = Path.Combine(channelsRoot, $"{channelName}-pairing.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(requests, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static List<string> ReadAllowlist(string channelsRoot, string channelName)
    {
        var path = Path.Combine(channelsRoot, $"{channelName}-allowlist.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void WriteAllowlist(string channelsRoot, string channelName, IReadOnlyList<string> values)
    {
        Directory.CreateDirectory(channelsRoot);
        var path = Path.Combine(channelsRoot, $"{channelName}-allowlist.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static string FormatUptime(string startedAt)
    {
        if (!DateTimeOffset.TryParse(startedAt, out var timestamp))
        {
            return string.Empty;
        }

        var duration = DateTimeOffset.UtcNow - timestamp.ToUniversalTime();
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{Math.Max(0, duration.Seconds)}s";
    }

    private static string GeneratePairingCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
    }

    private sealed class ChannelConfigAccumulator
    {
        /// <summary>
        /// Gets or sets the name
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the scope
        /// </summary>
        public string Scope { get; set; } = "user";

        /// <summary>
        /// Gets the properties
        /// </summary>
        public Dictionary<string, JsonElement> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Executes to configured channel
        /// </summary>
        /// <param name="projectRoot">The project root</param>
        /// <returns>The resulting configured channel</returns>
        public ConfiguredChannel ToConfiguredChannel(string projectRoot)
        {
            var type = GetString("type");
            var cwd = GetString("cwd");
            var groups = ReadGroups();
            var additionalSettings = Properties
                .Where(static item => !KnownProperties.Contains(item.Key))
                .ToDictionary(
                    static item => item.Key,
                    static item => item.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase);
            return new ConfiguredChannel(
                Name,
                Type: type,
                Scope,
                Description: GetString("description"),
                SenderPolicy: GetString("senderPolicy", "allowlist"),
                SessionScope: GetString("sessionScope", "user"),
                WorkingDirectory: ResolvePath(string.IsNullOrWhiteSpace(cwd) ? projectRoot : cwd, projectRoot),
                ApprovalMode: GetString("approvalMode"),
                Model: GetString("model"),
                Token: GetString("token"),
                ClientId: GetString("clientId"),
                ClientSecret: GetString("clientSecret"),
                BaseUrl: GetString("baseUrl"),
                Instructions: GetString("instructions"),
                GroupPolicy: GetString("groupPolicy", "disabled"),
                DispatchMode: GetString("dispatchMode", "collect"),
                BlockStreaming: GetString("blockStreaming", "off"),
                BlockStreamingChunk: ReadBlockStreamingChunk(),
                BlockStreamingCoalesce: ReadBlockStreamingCoalesce(),
                RequireMentionByDefault: groups.TryGetValue("*", out var defaults) ? defaults.RequireMention : true,
                Groups: groups.Values.OrderBy(static item => item.ChatId, StringComparer.OrdinalIgnoreCase).ToArray(),
                AdditionalSettings: additionalSettings);
        }

        private static readonly HashSet<string> KnownProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "type",
            "description",
            "senderPolicy",
            "sessionScope",
            "cwd",
            "approvalMode",
            "model",
            "token",
            "clientId",
            "clientSecret",
            "baseUrl",
            "instructions",
            "groupPolicy",
            "dispatchMode",
            "blockStreaming",
            "blockStreamingChunk",
            "blockStreamingCoalesce",
            "groups"
        };

        private string GetString(string key, string fallback = "")
        {
            if (!Properties.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return fallback;
            }

            return value.GetString() ?? fallback;
        }

        private Dictionary<string, ChannelGroupRuntimeConfiguration> ReadGroups()
        {
            if (!Properties.TryGetValue("groups", out var groupsElement) || groupsElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, ChannelGroupRuntimeConfiguration>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, ChannelGroupRuntimeConfiguration>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in groupsElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var requireMention = true;
                var dispatchMode = string.Empty;

                if (property.Value.TryGetProperty("requireMention", out var requireMentionElement))
                {
                    requireMention = requireMentionElement.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => true
                    };
                }

                if (property.Value.TryGetProperty("dispatchMode", out var dispatchModeElement) &&
                    dispatchModeElement.ValueKind == JsonValueKind.String)
                {
                    dispatchMode = dispatchModeElement.GetString() ?? string.Empty;
                }

                result[property.Name] = new ChannelGroupRuntimeConfiguration
                {
                    ChatId = property.Name,
                    RequireMention = requireMention,
                    DispatchMode = dispatchMode
                };
            }

            return result;
        }

        private ChannelBlockStreamingChunkConfiguration ReadBlockStreamingChunk()
        {
            if (!Properties.TryGetValue("blockStreamingChunk", out var element) || element.ValueKind != JsonValueKind.Object)
            {
                return new ChannelBlockStreamingChunkConfiguration();
            }

            var minChars = element.TryGetProperty("minChars", out var minElement) && minElement.ValueKind == JsonValueKind.Number
                ? minElement.GetInt32()
                : 400;
            var maxChars = element.TryGetProperty("maxChars", out var maxElement) && maxElement.ValueKind == JsonValueKind.Number
                ? maxElement.GetInt32()
                : 1000;

            return new ChannelBlockStreamingChunkConfiguration
            {
                MinChars = Math.Max(1, minChars),
                MaxChars = Math.Max(Math.Max(1, minChars), maxChars)
            };
        }

        private ChannelBlockStreamingCoalesceConfiguration ReadBlockStreamingCoalesce()
        {
            if (!Properties.TryGetValue("blockStreamingCoalesce", out var element) || element.ValueKind != JsonValueKind.Object)
            {
                return new ChannelBlockStreamingCoalesceConfiguration();
            }

            var idleMs = element.TryGetProperty("idleMs", out var idleElement) && idleElement.ValueKind == JsonValueKind.Number
                ? idleElement.GetInt32()
                : 1500;

            return new ChannelBlockStreamingCoalesceConfiguration
            {
                IdleMs = Math.Max(0, idleMs)
            };
        }

        private static string ResolvePath(string path, string projectRoot) =>
            Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(projectRoot, path));
    }

    private sealed record ConfiguredChannel(
        string Name,
        string Type,
        string Scope,
        string Description,
        string SenderPolicy,
        string SessionScope,
        string WorkingDirectory,
        string ApprovalMode,
        string Model,
        string Token,
        string ClientId,
        string ClientSecret,
        string BaseUrl,
        string Instructions,
        string GroupPolicy,
        string DispatchMode,
        string BlockStreaming,
        ChannelBlockStreamingChunkConfiguration BlockStreamingChunk,
        ChannelBlockStreamingCoalesceConfiguration BlockStreamingCoalesce,
        bool RequireMentionByDefault,
        IReadOnlyList<ChannelGroupRuntimeConfiguration> Groups,
        IReadOnlyDictionary<string, JsonElement> AdditionalSettings);

    private sealed class ChannelServiceInfo
    {
        /// <summary>
        /// Gets or sets the pid
        /// </summary>
        public int Pid { get; init; }

        /// <summary>
        /// Gets or sets the started at
        /// </summary>
        public string StartedAt { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the channels
        /// </summary>
        public string[] Channels { get; init; } = [];
    }

    private sealed class PairingRequestRecord
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
        /// Gets or sets the code
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the created at
        /// </summary>
        public long CreatedAt { get; init; }
    }
}
