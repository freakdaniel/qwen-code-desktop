using QwenCode.Core.Models;
using QwenCode.Core.Runtime;

namespace QwenCode.Core.Telemetry;

/// <summary>
/// Defines the contract for Telemetry Service
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Executes track session configured async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task TrackSessionConfiguredAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes track user prompt async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="promptId">The prompt id</param>
    /// <param name="prompt">The prompt content</param>
    /// <param name="authType">The auth type</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task TrackUserPromptAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        string promptId,
        string prompt,
        string authType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes track api request async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="providerName">The provider name</param>
    /// <param name="model">The model</param>
    /// <param name="durationMs">The duration ms</param>
    /// <param name="status">The status</param>
    /// <param name="statusCode">The status code</param>
    /// <param name="errorType">The error type</param>
    /// <param name="isStreaming">The is streaming</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
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

    /// <summary>
    /// Executes track tool call async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="toolCall">The tool call</param>
    /// <param name="execution">The execution</param>
    /// <param name="durationMs">The duration ms</param>
    /// <param name="toolType">The tool type</param>
    /// <param name="decision">The decision</param>
    /// <param name="mcpServerName">The mcp server name</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
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

    /// <summary>
    /// Executes track chat compression async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="sessionId">The session identifier</param>
    /// <param name="checkpoint">The checkpoint</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task TrackChatCompressionAsync(
        QwenRuntimeProfile runtimeProfile,
        string sessionId,
        ChatCompressionCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes track subagent execution async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="executionId">The execution id</param>
    /// <param name="agentName">The agent name</param>
    /// <param name="status">The status</param>
    /// <param name="durationMs">The duration ms</param>
    /// <param name="model">The model</param>
    /// <param name="stopReason">The stop reason</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task TrackSubagentExecutionAsync(
        QwenRuntimeProfile runtimeProfile,
        string executionId,
        string agentName,
        string status,
        long durationMs,
        string model,
        string stopReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes publish async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="record">The record</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task PublishAsync(
        QwenRuntimeProfile runtimeProfile,
        TelemetryEventRecord record,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes increment async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="metricName">The metric name</param>
    /// <param name="value">The value</param>
    /// <param name="unit">The unit</param>
    /// <param name="tags">The tags</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task IncrementAsync(
        QwenRuntimeProfile runtimeProfile,
        string metricName,
        double value = 1,
        string unit = "count",
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshot async
    /// </summary>
    /// <param name="runtimeProfile">The runtime profile</param>
    /// <param name="cancellationToken">The token that can be used to cancel the operation</param>
    /// <returns>A task that resolves to telemetry snapshot</returns>
    Task<TelemetrySnapshot> GetSnapshotAsync(
        QwenRuntimeProfile runtimeProfile,
        CancellationToken cancellationToken = default);
}
