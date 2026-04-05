namespace QwenCode.App.Models;

/// <summary>
/// Represents the Ide Tool Call Result
/// </summary>
public sealed class IdeToolCallResult
{
    /// <summary>
    /// Gets or sets a value indicating whether is error
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Gets or sets the text
    /// </summary>
    public string Text { get; init; } = string.Empty;
}
