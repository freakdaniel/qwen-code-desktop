using QwenCode.Core.Models;

namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Assistant Turn Request
/// </summary>
public sealed class AssistantTurnRequest
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
    /// Gets or sets the git branch
    /// </summary>
    public string GitBranch { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the desktop surface context that started this turn.
    /// </summary>
    public string SurfaceContext { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the command invocation
    /// </summary>
    public CommandInvocationResult? CommandInvocation { get; init; }

    /// <summary>
    /// Gets or sets the resolved command
    /// </summary>
    public ResolvedCommand? ResolvedCommand { get; init; }

    /// <summary>
    /// Gets or sets the tool execution
    /// </summary>
    public required NativeToolExecutionResult ToolExecution { get; init; }

    /// <summary>
    /// Gets or sets the arguments json for a pre-executed tool result.
    /// </summary>
    public string ToolArgumentsJson { get; init; } = "{}";

    /// <summary>
    /// Gets or sets a value indicating whether is approval resolution
    /// </summary>
    public bool IsApprovalResolution { get; init; }

    /// <summary>
    /// Gets or sets the prompt mode
    /// </summary>
    public AssistantPromptMode PromptMode { get; init; } = AssistantPromptMode.Primary;

    /// <summary>
    /// Gets or sets the system prompt override
    /// </summary>
    public string SystemPromptOverride { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the allowed tool names
    /// </summary>
    public IReadOnlyList<string> AllowedToolNames { get; init; } = [];

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
    /// Gets or sets the disable tools
    /// </summary>
    public bool DisableTools { get; init; }
}
