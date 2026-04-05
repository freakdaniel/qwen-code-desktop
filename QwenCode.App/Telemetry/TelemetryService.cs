using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Telemetry;

public sealed class TelemetryService(ILogger<TelemetryService> logger) : ITelemetryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] RedactedPropertyMarkers =
    [
        "prompt",
        "content",
        "message",
        "messages",
        "system",
        "resolved",
        "history",
        "context"
    ];

    public Task TrackSessionConfiguredAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["model"] = runtimeProfile.ModelName,
            ["approval_mode"] = runtimeProfile.ApprovalProfile.DefaultMode,
            ["runtime_source"] = runtimeProfile.RuntimeSource,
            ["checkpointing"] = runtimeProfile.Checkpointing,
            ["folder_trust_enabled"] = runtimeProfile.FolderTrustEnabled,
            ["workspace_trusted"] = runtimeProfile.IsWorkspaceTrusted,
            ["context_file_count"] = runtimeProfile.ContextFileNames.Count,
            ["chat_compression_enabled"] = runtimeProfile.ChatCompression is not null
        };

        return PublishCounterEventAsync(
            runtimeProfile,
            new TelemetryEventRecord
            {
                EventName = "cli_config",
                SessionId = sessionId,
                Payload = payload
            },
            "qwen.session.count",
            cancellationToken);
    }

    public Task TrackUserPromptAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        string promptId,
        string prompt,
        string authType,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["prompt_id"] = promptId,
            ["prompt_length"] = prompt?.Length ?? 0,
            ["auth_type"] = authType,
            ["prompt"] = prompt ?? string.Empty
        };

        return PublishCounterEventAsync(
            runtimeProfile,
            new TelemetryEventRecord
            {
                EventName = "user_prompt",
                SessionId = sessionId,
                Payload = payload
            },
            "qwen.prompt.count",
            cancellationToken);
    }

    public async Task TrackApiRequestAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        string providerName,
        string model,
        long durationMs,
        string status,
        int? statusCode = null,
        string? errorType = null,
        bool isStreaming = false,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["provider_name"] = providerName,
            ["model"] = model,
            ["duration_ms"] = durationMs,
            ["status"] = status,
            ["status_code"] = statusCode,
            ["error_type"] = errorType ?? string.Empty,
            ["streaming"] = isStreaming
        };

        await PublishAsync(
            runtimeProfile,
            new TelemetryEventRecord
            {
                EventName = "api_request",
                SessionId = sessionId,
                Payload = payload
            },
            cancellationToken);
        await IncrementAsync(
            runtimeProfile,
            "qwen.api.request.count",
            1,
            tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = providerName,
                ["model"] = model,
                ["status"] = status
            },
            cancellationToken: cancellationToken);
        await IncrementAsync(
            runtimeProfile,
            "qwen.api.request.latency",
            durationMs,
            "ms",
            tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["provider"] = providerName,
                ["model"] = model
            },
            cancellationToken);
    }

    public async Task TrackToolCallAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        AssistantToolCall toolCall,
        NativeToolExecutionResult execution,
        long durationMs,
        string toolType = "native",
        string? decision = null,
        string? mcpServerName = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["function_name"] = toolCall.ToolName,
            ["function_args"] = SafeParseArguments(toolCall.ArgumentsJson),
            ["duration_ms"] = durationMs,
            ["status"] = execution.Status,
            ["success"] = string.Equals(execution.Status, "completed", StringComparison.OrdinalIgnoreCase),
            ["decision"] = decision ?? string.Empty,
            ["tool_type"] = toolType,
            ["approval_state"] = execution.ApprovalState,
            ["error"] = execution.ErrorMessage ?? string.Empty,
            ["content_length"] = string.IsNullOrWhiteSpace(execution.Output) ? 0 : execution.Output.Length,
            ["mcp_server_name"] = mcpServerName ?? string.Empty,
            ["changed_files_count"] = execution.ChangedFiles.Count
        };

        await PublishAsync(
            runtimeProfile,
            new TelemetryEventRecord
            {
                EventName = "tool_call",
                SessionId = sessionId,
                Payload = payload
            },
            cancellationToken);
        await IncrementAsync(
            runtimeProfile,
            "qwen.tool.call.count",
            1,
            tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tool"] = toolCall.ToolName,
                ["status"] = execution.Status,
                ["tool_type"] = toolType
            },
            cancellationToken: cancellationToken);
        await IncrementAsync(
            runtimeProfile,
            "qwen.tool.call.latency",
            durationMs,
            "ms",
            tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tool"] = toolCall.ToolName
            },
            cancellationToken);
    }

    public async Task TrackChatCompressionAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        ChatCompressionCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["tokens_before"] = checkpoint.EstimatedTokenCount,
            ["tokens_after"] = Math.Max(0, checkpoint.EstimatedTokenCount - (int)Math.Round(checkpoint.EstimatedTokenCount * checkpoint.ThresholdPercentage)),
            ["compressed_entries"] = checkpoint.CompressedEntryCount,
            ["preserved_entries"] = checkpoint.PreservedEntryCount,
            ["context_percentage"] = checkpoint.EstimatedContextPercentage,
            ["threshold_percentage"] = checkpoint.ThresholdPercentage,
            ["trigger"] = checkpoint.Trigger
        };

        await PublishAsync(
            runtimeProfile,
            new TelemetryEventRecord
            {
                EventName = "chat_compression",
                SessionId = sessionId,
                Payload = payload
            },
            cancellationToken);
        await IncrementAsync(
            runtimeProfile,
            "qwen.chat.compression.count",
            1,
            tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["trigger"] = checkpoint.Trigger
            },
            cancellationToken: cancellationToken);
    }

    public async Task TrackSubagentExecutionAsync(
        QwenRuntimeProfile runtimeProfile,
        string executionId,
        string agentName,
        string status,
        long durationMs,
        string model,
        string stopReason,
        CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["agent_name"] = agentName,
            ["status"] = status,
            ["duration_ms"] = durationMs,
            ["model"] = model,
            ["stop_reason"] = stopReason
        };

        await PublishAsync(
            runtimeProfile,
            new TelemetryEventRecord
            {
                EventName = "subagent_execution",
                SessionId = executionId,
                Payload = payload
            },
            cancellationToken);
        await IncrementAsync(
            runtimeProfile,
            "qwen.subagent.execution.count",
            1,
            tags: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["agent"] = agentName,
                ["status"] = status
            },
            cancellationToken: cancellationToken);
    }

    public async Task PublishAsync(
        QwenRuntimeProfile runtimeProfile,
        TelemetryEventRecord record,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(runtimeProfile))
        {
            return;
        }

        var paths = ResolvePaths(runtimeProfile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.EventsPath)!);

        var envelope = new JsonObject
        {
            ["event.name"] = record.EventName,
            ["event.timestamp"] = record.TimestampUtc.ToUniversalTime().ToString("O"),
            ["session_id"] = record.SessionId
        };

        foreach (var property in SanitizePayload(runtimeProfile, record.Payload))
        {
            envelope[property.Key] = property.Value?.DeepClone();
        }

        var line = envelope.ToJsonString();
        await ExecuteSerializedAsync(
            paths.EventsPath,
            async () =>
            {
                await File.AppendAllTextAsync(paths.EventsPath, line + Environment.NewLine, cancellationToken);
            });
    }

    public async Task IncrementAsync(
        QwenRuntimeProfile runtimeProfile,
        string metricName,
        double value = 1,
        string unit = "count",
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(runtimeProfile))
        {
            return;
        }

        foreach (var expandedMetricName in ExpandMetricAliases(metricName))
        {
            await IncrementMetricUnsafeAsync(runtimeProfile, expandedMetricName, value, unit, tags, cancellationToken);
        }
    }

    private async Task IncrementMetricUnsafeAsync(
        QwenRuntimeProfile runtimeProfile,
        string metricName,
        double value,
        string unit,
        IReadOnlyDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        var paths = ResolvePaths(runtimeProfile);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.MetricsPath)!);

        await ExecuteSerializedAsync(
            paths.MetricsPath,
            async () =>
            {
                var metrics = await ReadMetricsUnsafeAsync(paths.MetricsPath, cancellationToken);
                metrics.TryGetValue(metricName, out var existing);
                var count = (existing?.Count ?? 0) + 1;
                var sum = (existing?.Sum ?? 0d) + value;
                var min = existing is null ? value : Math.Min(existing.Min, value);
                var max = existing is null ? value : Math.Max(existing.Max, value);

                metrics[metricName] = new TelemetryMetricAggregate
                {
                    Count = count,
                    Sum = sum,
                    Min = min,
                    Max = max,
                    Unit = unit,
                    LastTags = tags is null
                        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase),
                    LastUpdatedUtc = DateTime.UtcNow
                };

                await File.WriteAllTextAsync(paths.MetricsPath, JsonSerializer.Serialize(metrics, JsonOptions), cancellationToken);
            });
    }

    public async Task<TelemetrySnapshot> GetSnapshotAsync(
        QwenRuntimeProfile runtimeProfile,
        CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(runtimeProfile);
        var eventCount = 0;
        if (File.Exists(paths.EventsPath))
        {
            eventCount = await Task.Run(
                () => File.ReadLines(paths.EventsPath).Count(static line => !string.IsNullOrWhiteSpace(line)),
                cancellationToken);
        }

        var metrics = await ReadMetricsUnsafeAsync(paths.MetricsPath, cancellationToken);
        return new TelemetrySnapshot
        {
            EventsPath = paths.EventsPath,
            MetricsPath = paths.MetricsPath,
            EventCount = eventCount,
            Metrics = metrics
        };
    }

    private async Task PublishCounterEventAsync(
        QwenRuntimeProfile runtimeProfile,
        TelemetryEventRecord record,
        string metricName,
        CancellationToken cancellationToken)
    {
        await PublishAsync(runtimeProfile, record, cancellationToken);
        await IncrementAsync(runtimeProfile, metricName, cancellationToken: cancellationToken);
    }

    private static bool IsEnabled(QwenRuntimeProfile runtimeProfile)
    {
        var settings = runtimeProfile.Telemetry;
        return settings is not null &&
               (settings.Enabled ||
                !string.IsNullOrWhiteSpace(settings.Target) ||
                !string.IsNullOrWhiteSpace(settings.OtlpEndpoint) ||
                !string.IsNullOrWhiteSpace(settings.Outfile));
    }

    private static JsonObject SanitizePayload(QwenRuntimeProfile runtimeProfile, JsonObject payload)
    {
        var clone = payload.DeepClone() as JsonObject ?? [];
        if (runtimeProfile.Telemetry?.LogPrompts == true)
        {
            return clone;
        }

        RedactNode(clone);
        return clone;
    }

    private static void RedactNode(JsonNode? node, string propertyName = "")
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToArray())
                {
                    if (ShouldRedact(property.Key))
                    {
                        obj[property.Key] = "[redacted]";
                        continue;
                    }

                    RedactNode(property.Value, property.Key);
                }

                break;

            case JsonArray array when ShouldRedact(propertyName):
                array.Clear();
                array.Add("[redacted]");
                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    RedactNode(item, propertyName);
                }

                break;
        }
    }

    private static bool ShouldRedact(string propertyName) =>
        RedactedPropertyMarkers.Any(marker =>
            propertyName.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static JsonNode SafeParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(argumentsJson)?.DeepClone() ?? new JsonObject();
        }
        catch
        {
            return argumentsJson;
        }
    }

    private async Task ExecuteSerializedAsync(string key, Func<Task> action)
    {
        var semaphore = FileLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Telemetry write failed for {TelemetryTarget}", key);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task<Dictionary<string, TelemetryMetricAggregate>> ReadMetricsUnsafeAsync(
        string metricsPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(metricsPath))
        {
            return new Dictionary<string, TelemetryMetricAggregate>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var content = await File.ReadAllTextAsync(metricsPath, cancellationToken);
            return JsonSerializer.Deserialize<Dictionary<string, TelemetryMetricAggregate>>(content) ??
                   new Dictionary<string, TelemetryMetricAggregate>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, TelemetryMetricAggregate>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static (string EventsPath, string MetricsPath) ResolvePaths(QwenRuntimeProfile runtimeProfile)
    {
        var configuredOutfile = runtimeProfile.Telemetry?.Outfile;
        var eventsPath = string.IsNullOrWhiteSpace(configuredOutfile)
            ? Path.Combine(runtimeProfile.RuntimeBaseDirectory, "telemetry", "events.jsonl")
            : ResolvePath(configuredOutfile, runtimeProfile.ProjectRoot);
        var directory = Path.GetDirectoryName(eventsPath) ?? Path.Combine(runtimeProfile.RuntimeBaseDirectory, "telemetry");
        var baseName = Path.GetFileNameWithoutExtension(eventsPath);
        var metricsPath = Path.Combine(directory, $"{baseName}.metrics.json");
        return (eventsPath, metricsPath);
    }

    private static string ResolvePath(string value, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (Path.IsPathRooted(value))
        {
            return Path.GetFullPath(value);
        }

        return Path.GetFullPath(Path.Combine(projectRoot, value));
    }

    private static IReadOnlyList<string> ExpandMetricAliases(string metricName)
    {
        if (!metricName.StartsWith("qwen.", StringComparison.OrdinalIgnoreCase))
        {
            return [metricName];
        }

        return [metricName, metricName["qwen.".Length..]];
    }
}
