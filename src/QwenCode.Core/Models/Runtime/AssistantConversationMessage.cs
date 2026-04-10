namespace QwenCode.Core.Runtime;

/// <summary>
/// Represents the Assistant Conversation Message
/// </summary>
public sealed class AssistantConversationMessage
{
    /// <summary>
    /// Gets or sets the role
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets or sets the content
    /// </summary>
    public required string Content { get; init; }
}
