namespace QwenCode.App.Models;

public sealed class IdeToolCallResult
{
    public bool IsError { get; init; }

    public string Text { get; init; } = string.Empty;
}
