namespace QwenCode.Core.Models;

/// <summary>
/// Represents the Mcp Tool Definition
/// </summary>
public sealed class McpToolDefinition
{
    /// <summary>
    /// Gets or sets the server name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// Gets or sets the name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the fully qualified name
    /// </summary>
    public required string FullyQualifiedName { get; init; }

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the input schema json
    /// </summary>
    public string InputSchemaJson { get; init; } = "{}";

    /// <summary>
    /// Gets or sets the read only hint
    /// </summary>
    public bool ReadOnlyHint { get; init; }

    /// <summary>
    /// Gets or sets the destructive hint
    /// </summary>
    public bool DestructiveHint { get; init; }

    /// <summary>
    /// Gets or sets the idempotent hint
    /// </summary>
    public bool IdempotentHint { get; init; }

    /// <summary>
    /// Gets or sets the open world hint
    /// </summary>
    public bool OpenWorldHint { get; init; }
}
