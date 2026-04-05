namespace QwenCode.App.Runtime;

public sealed class AssistantConversationMessage
{
    public required string Role { get; init; }

    public required string Content { get; init; }
}
