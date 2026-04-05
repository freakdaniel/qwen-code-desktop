using System.Text.Json.Nodes;

namespace QwenCode.App.Models;

public sealed class HookInvocationRequest
{
    public required HookEventName EventName { get; init; }

    public required string SessionId { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string TranscriptPath { get; init; }

    public string Prompt { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string ToolStatus { get; init; } = string.Empty;

    public string ApprovalState { get; init; } = string.Empty;

    public string ToolArgumentsJson { get; init; } = string.Empty;

    public string ToolOutput { get; init; } = string.Empty;

    public string ToolErrorMessage { get; init; } = string.Empty;

    public string AgentName { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public JsonObject Metadata { get; init; } = [];
}
