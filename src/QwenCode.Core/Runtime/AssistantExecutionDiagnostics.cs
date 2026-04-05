namespace QwenCode.App.Runtime;

internal static class AssistantExecutionDiagnostics
{
    /// <summary>
    /// Builds stats
    /// </summary>
    /// <param name="roundCount">The round count</param>
    /// <param name="toolExecutions">The tool executions</param>
    /// <param name="durationMs">The duration ms</param>
    /// <returns>The resulting assistant execution stats</returns>
    public static AssistantExecutionStats BuildStats(
        int roundCount,
        IReadOnlyList<AssistantToolCallResult> toolExecutions,
        long durationMs = 0)
    {
        var successfulToolCalls = toolExecutions.Count(static item =>
            string.Equals(item.Execution.Status, "completed", StringComparison.OrdinalIgnoreCase));
        return new AssistantExecutionStats
        {
            RoundCount = Math.Max(0, roundCount),
            ToolCallCount = toolExecutions.Count,
            SuccessfulToolCallCount = successfulToolCalls,
            FailedToolCallCount = Math.Max(0, toolExecutions.Count - successfulToolCalls),
            DurationMs = Math.Max(0, durationMs)
        };
    }

    /// <summary>
    /// Resolves stats
    /// </summary>
    /// <param name="response">The response payload</param>
    /// <param name="startedAtUtc">The started at utc</param>
    /// <param name="endedAtUtc">The ended at utc</param>
    /// <param name="defaultRoundCount">The default round count</param>
    /// <returns>The resulting assistant execution stats</returns>
    public static AssistantExecutionStats ResolveStats(
        AssistantTurnResponse response,
        DateTime startedAtUtc,
        DateTime endedAtUtc,
        int defaultRoundCount = 1)
    {
        var resolvedDurationMs = Math.Max(0L, (long)(endedAtUtc - startedAtUtc).TotalMilliseconds);
        var stats = response.Stats;
        if (stats.RoundCount > 0 || stats.ToolCallCount > 0 || stats.DurationMs > 0)
        {
            return new AssistantExecutionStats
            {
                RoundCount = stats.RoundCount > 0 ? stats.RoundCount : Math.Max(1, defaultRoundCount),
                ToolCallCount = stats.ToolCallCount,
                SuccessfulToolCallCount = stats.SuccessfulToolCallCount,
                FailedToolCallCount = stats.FailedToolCallCount,
                DurationMs = stats.DurationMs > 0 ? stats.DurationMs : resolvedDurationMs
            };
        }

        return BuildStats(Math.Max(1, defaultRoundCount), response.ToolExecutions, resolvedDurationMs);
    }

    /// <summary>
    /// Resolves stop reason
    /// </summary>
    /// <param name="response">The response payload</param>
    /// <returns>The resulting string</returns>
    public static string ResolveStopReason(AssistantTurnResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.StopReason))
        {
            return response.StopReason;
        }

        var terminalToolStatus = response.ToolExecutions.LastOrDefault()?.Execution.Status;
        return ResolveStopReasonFromStatus(terminalToolStatus);
    }

    /// <summary>
    /// Resolves stop reason from status
    /// </summary>
    /// <param name="status">The status</param>
    /// <returns>The resulting string</returns>
    public static string ResolveStopReasonFromStatus(string? status) =>
        status switch
        {
            "approval-required" => "tool-approval-required",
            "input-required" => "tool-input-required",
            "blocked" => "tool-blocked",
            "error" => "tool-error",
            "cancelled" => "cancelled",
            _ => "completed"
        };
}
