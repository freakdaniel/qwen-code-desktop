namespace QwenCode.App.Models;

/// <summary>
/// Represents the Mcp Tool Invocation Result
/// </summary>
public sealed class McpToolInvocationResult
{
    /// <summary>
    /// Gets or sets the server name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the tool name
    /// </summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets or sets the output
    /// </summary>
    public required string Output { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether is error
    /// </summary>
    public bool IsError { get; init; }
}
