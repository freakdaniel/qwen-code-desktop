using System.Security.Cryptography;
using System.Text;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class LoopDetectionService : ILoopDetectionService
{
    private const int ConsecutiveToolCallThreshold = 5;
    private const int ContentLoopThreshold = 10;
    private const int ContentChunkSize = 50;
    private const int MaxHistoryLength = 1000;

    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, SessionLoopState> sessions = new(StringComparer.Ordinal);

    public void Reset(string sessionId)
    {
        lock (syncRoot)
        {
            sessions[sessionId] = new SessionLoopState();
        }
    }

    public LoopDetectionDecision ObserveToolCall(string sessionId, AssistantToolCall toolCall)
    {
        lock (syncRoot)
        {
            var state = GetOrCreateState(sessionId);
            var key = ComputeHash($"{toolCall.ToolName}:{toolCall.ArgumentsJson}");
            if (string.Equals(state.LastToolCallKey, key, StringComparison.Ordinal))
            {
                state.ToolCallRepetitionCount++;
            }
            else
            {
                state.LastToolCallKey = key;
                state.ToolCallRepetitionCount = 1;
            }

            if (state.ToolCallRepetitionCount >= ConsecutiveToolCallThreshold)
            {
                return new LoopDetectionDecision
                {
                    IsDetected = true,
                    LoopType = "consecutive-identical-tool-calls",
                    Reason = $"Detected {state.ToolCallRepetitionCount} consecutive identical tool calls for '{toolCall.ToolName}'."
                };
            }

            return new LoopDetectionDecision();
        }
    }

    public LoopDetectionDecision ObserveContentDelta(string sessionId, string contentDelta)
    {
        if (string.IsNullOrWhiteSpace(contentDelta))
        {
            return new LoopDetectionDecision();
        }

        lock (syncRoot)
        {
            var state = GetOrCreateState(sessionId);
            state.StreamContentHistory += contentDelta;
            if (state.StreamContentHistory.Length > MaxHistoryLength)
            {
                state.StreamContentHistory = state.StreamContentHistory[^MaxHistoryLength..];
                state.LastContentIndex = Math.Min(state.LastContentIndex, state.StreamContentHistory.Length);
            }

            while (state.LastContentIndex + ContentChunkSize <= state.StreamContentHistory.Length)
            {
                var chunk = state.StreamContentHistory.Substring(state.LastContentIndex, ContentChunkSize);
                var hash = ComputeHash(chunk);
                if (!state.ContentOccurrences.TryGetValue(hash, out var indices))
                {
                    indices = [];
                    state.ContentOccurrences[hash] = indices;
                }

                indices.Add(state.LastContentIndex);
                if (indices.Count >= ContentLoopThreshold)
                {
                    var recent = indices[^ContentLoopThreshold..];
                    var totalDistance = recent[^1] - recent[0];
                    var averageDistance = totalDistance / (double)(ContentLoopThreshold - 1);
                    if (averageDistance <= ContentChunkSize * 1.5)
                    {
                        return new LoopDetectionDecision
                        {
                            IsDetected = true,
                            LoopType = "chanting-identical-content",
                            Reason = "Detected a repeated content pattern while streaming assistant output."
                        };
                    }
                }

                state.LastContentIndex++;
            }

            return new LoopDetectionDecision();
        }
    }

    public void Complete(string sessionId)
    {
        lock (syncRoot)
        {
            sessions.Remove(sessionId);
        }
    }

    private SessionLoopState GetOrCreateState(string sessionId)
    {
        if (!sessions.TryGetValue(sessionId, out var state))
        {
            state = new SessionLoopState();
            sessions[sessionId] = state;
        }

        return state;
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private sealed class SessionLoopState
    {
        public string LastToolCallKey { get; set; } = string.Empty;

        public int ToolCallRepetitionCount { get; set; }

        public string StreamContentHistory { get; set; } = string.Empty;

        public int LastContentIndex { get; set; }

        public Dictionary<string, List<int>> ContentOccurrences { get; } = new(StringComparer.Ordinal);
    }
}
