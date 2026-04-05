using QwenCode.App.Models;
using QwenCode.App.Runtime;

namespace QwenCode.App.Telemetry;

public interface ITelemetryService
{
    Task TrackSessionConfiguredAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task TrackUserPromptAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        string promptId,
        string prompt,
        string authType,
        CancellationToken cancellationToken = default);

    Task TrackApiRequestAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        string providerName,
        string model,
        long durationMs,
        string status,
        int? statusCode = null,
        string? errorType = null,
        bool isStreaming = false,
        CancellationToken cancellationToken = default);

    Task TrackToolCallAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        AssistantToolCall toolCall,
        NativeToolExecutionResult execution,
        long durationMs,
        string toolType = "native",
        string? decision = null,
        string? mcpServerName = null,
        CancellationToken cancellationToken = default);

    Task TrackChatCompressionAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        ChatCompressionCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    Task TrackSubagentExecutionAsync(
        QwenRuntimeProfile runtimeProfile,
        string executionId,
        string agentName,
        string status,
        long durationMs,
        string model,
        string stopReason,
        CancellationToken cancellationToken = default);

    Task PublishAsync(
        QwenRuntimeProfile runtimeProfile,
        TelemetryEventRecord record,
        CancellationToken cancellationToken = default);

    Task IncrementAsync(
        QwenRuntimeProfile runtimeProfile,
        string metricName,
        double value = 1,
        string unit = "count",
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    Task<TelemetrySnapshot> GetSnapshotAsync(
        QwenRuntimeProfile runtimeProfile,
        CancellationToken cancellationToken = default);
}
