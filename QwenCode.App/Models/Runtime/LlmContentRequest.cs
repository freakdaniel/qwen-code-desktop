using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

public sealed class LlmContentRequest
{
    public required string SessionId { get; init; }

    public required string Prompt { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string TranscriptPath { get; init; }

    public required QwenRuntimeProfile RuntimeProfile { get; init; }

    public required AssistantPromptContext PromptContext { get; init; }

    public string GitBranch { get; init; } = string.Empty;

    public string SystemPrompt { get; init; } = string.Empty;

    public string ModelOverride { get; init; } = string.Empty;

    public string AuthTypeOverride { get; init; } = string.Empty;

    public string EndpointOverride { get; init; } = string.Empty;

    public string ApiKeyOverride { get; init; } = string.Empty;

    public double? TemperatureOverride { get; init; }

    public bool DisableTools { get; init; } = true;

    public JsonObject? Metadata { get; init; }
}
