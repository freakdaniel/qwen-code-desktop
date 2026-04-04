using System.Diagnostics;
using System.Text.Json;
using QwenCode.App.Compatibility;
using QwenCode.App.Config;
using QwenCode.App.Extensions;
using QwenCode.App.Infrastructure;
using QwenCode.App.Models;

namespace QwenCode.App.Channels;

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

    public ChannelPairingSnapshot GetPairings(WorkspacePaths workspace, GetChannelPairingRequest request)
    {
        var runtime = settingsResolver.InspectRuntimeProfile(workspace);
        EnsureChannelExists(runtime, workspace, request.Name);
        return BuildPairingSnapshot(request.Name);
    }

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
                if (!entry.Value.TryGetProperty("target", out var target) ||
                    target.ValueKind != JsonValueKind.Object ||
                    !target.TryGetProperty("channelName", out var channelNameElement) ||
                    channelNameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var channelName = channelNameElement.GetString();
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

    private sealed class ChannelConfigAccumulator
    {
        public required string Name { get; init; }

        public string Scope { get; set; } = "user";

        public Dictionary<string, JsonElement> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ConfiguredChannel ToConfiguredChannel(string projectRoot)
        {
            var type = GetString("type");
            var cwd = GetString("cwd");
            return new ConfiguredChannel(
                Name,
                Type: type,
                Scope,
                Description: GetString("description"),
                SenderPolicy: GetString("senderPolicy", "allowlist"),
                SessionScope: GetString("sessionScope", "user"),
                WorkingDirectory: ResolvePath(string.IsNullOrWhiteSpace(cwd) ? projectRoot : cwd, projectRoot),
                ApprovalMode: GetString("approvalMode"),
                Model: GetString("model"));
        }

        private string GetString(string key, string fallback = "")
        {
            if (!Properties.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return fallback;
            }

            return value.GetString() ?? fallback;
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
        string Model);

    private sealed class ChannelServiceInfo
    {
        public int Pid { get; init; }

        public string StartedAt { get; init; } = string.Empty;

        public string[] Channels { get; init; } = [];
    }

    private sealed class PairingRequestRecord
    {
        public string SenderId { get; init; } = string.Empty;

        public string SenderName { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public long CreatedAt { get; init; }
    }
}
