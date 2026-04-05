namespace QwenCode.App.Models;

/// <summary>
/// Represents the Mcp Prompt Invocation Result
/// </summary>
public sealed class McpPromptInvocationResult
{
    /// <summary>
    /// Gets or sets the server name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the prompt name
    /// </summary>
    public required string PromptName { get; init; }

    /// <summary>
    /// Gets or sets the output
    /// </summary>
    public string Output { get; init; } = string.Empty;
}
