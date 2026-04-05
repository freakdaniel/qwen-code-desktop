using System.Text.Json.Nodes;
using QwenCode.App.Models;

namespace QwenCode.App.Runtime;

/// <summary>
/// Represents the Llm Content Request
/// </summary>
public sealed class LlmContentRequest
{
    /// <summary>
    /// Gets or sets the session id
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets or sets the prompt
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets or sets the working directory
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets or sets the transcript path
    /// </summary>
    public required string TranscriptPath { get; init; }

    /// <summary>
    /// Gets or sets the runtime profile
    /// </summary>
    public required QwenRuntimeProfile RuntimeProfile { get; init; }

    /// <summary>
    /// Gets or sets the prompt context
    /// </summary>
    public required AssistantPromptContext PromptContext { get; init; }

    /// <summary>
    /// Gets or sets the git branch
    /// </summary>
    public string GitBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the system prompt
    /// </summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the model override
    /// </summary>
    public string ModelOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the auth type override
    /// </summary>
    public string AuthTypeOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint override
    /// </summary>
    public string EndpointOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the api key override
    /// </summary>
    public string ApiKeyOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the temperature override
    /// </summary>
    public double? TemperatureOverride { get; init; }

    /// <summary>
    /// Gets or sets the disable tools
    /// </summary>
    public bool DisableTools { get; init; } = true;

    /// <summary>
    /// Gets or sets the metadata
    /// </summary>
    public JsonObject? Metadata { get; init; }
}
